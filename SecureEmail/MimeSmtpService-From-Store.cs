using MailKit.Net.Smtp;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using MimeKit.Cryptography;
using MimeKit.IO;
using MimeKit.Text;
using System.Net.Mail;
using System.Security.Cryptography.X509Certificates;
using Umbraco.Cms.Core.Configuration.Models;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Mail;

using SecureSocketOptions = MailKit.Security.SecureSocketOptions;
using SmtpClient = MailKit.Net.Smtp.SmtpClient;

namespace S3.Utilities.SecureEmail {
    public class MimeSmtpService : IMimeSmtpService {

        private const string EMAIL_EXTENSION = ".eml";
        private const string FORMS_BCC_EMAIL_ADDR = "S3:Forms:FormsInboxEmailAddress";
        private const string CERT_SIGNER_EMAIL = "S3:Forms:CertificateSignerEmail";

        private readonly IEventAggregator _eventAggregator;
        private readonly ILogger<MimeSmtpService> _logger;
        private readonly IEmailSender _emailSender;
        private GlobalSettings _globalSettings;
        private ContentSettings _contentSettings;
        private readonly IConfiguration _configuration;

        public MimeSmtpService(
            IEventAggregator eventAggregator,
            ILogger<MimeSmtpService> logger,
            IEmailSender emailSender,
            IOptionsMonitor<GlobalSettings> globalSettings,
            IOptions<ContentSettings> contentSettings,
            IConfiguration configuration) {
            _eventAggregator = eventAggregator;
            _logger = logger;
            _emailSender = emailSender;
            _globalSettings = globalSettings.CurrentValue;
            _contentSettings = contentSettings.Value;
            _configuration = configuration;
        }

        public bool CanSendRequiredEmail() { 
            return _emailSender.CanSendRequiredEmail();
        }

        /// <summary>
        ///     Sends the message async
        /// </summary>
        /// <returns></returns>
        public async Task SendAsync(MimeMessage mm, string emailType) {
            if (!_globalSettings.IsSmtpServerConfigured && !_globalSettings.IsPickupDirectoryLocationConfigured) {
                if (_logger.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Debug)) {
                    _logger.LogDebug($"Could not send email for {mm.Subject}. It was not handled by a notification handler and there is no SMTP configured.");
                }
                return;
            }

            if (_globalSettings.IsPickupDirectoryLocationConfigured &&
                !string.IsNullOrWhiteSpace(_globalSettings.Smtp?.From)) {
                // The following code snippet is the recommended way to handle PickupDirectoryLocation.
                // See more https://github.com/jstedfast/MailKit/blob/master/FAQ.md#q-how-can-i-send-email-to-a-specifiedpickupdirectory
                do {
                    var path = Path.Combine(_globalSettings.Smtp.PickupDirectoryLocation!, Guid.NewGuid() + EMAIL_EXTENSION);
                    Stream stream;

                    try {
                        stream = File.Open(path, FileMode.CreateNew);
                    }
                    catch (IOException) {
                        if (File.Exists(path)) {
                            continue;
                        }

                        throw;
                    }

                    try {
                        using (stream) {
                            using var filtered = new FilteredStream(stream);
                            filtered.Add(new SmtpDataFilter());

                            FormatOptions options = FormatOptions.Default.Clone();
                            options.NewLineFormat = NewLineFormat.Dos;

                            //await message.ToMimeMessage(_globalSettings.Smtp.From).WriteToAsync(options, filtered);
                            await mm.WriteToAsync(options, filtered);

                            filtered.Flush();
                            return;
                        }
                    }
                    catch {
                        File.Delete(path);
                        throw;
                    }
                }
                while (true);
            }

            using var client = new SmtpClient();

            await client.ConnectAsync(
                _globalSettings.Smtp!.Host,
                _globalSettings.Smtp.Port,
                (SecureSocketOptions)(int)_globalSettings.Smtp.SecureSocketOptions);

            if (!string.IsNullOrWhiteSpace(_globalSettings.Smtp.Username) &&
                !string.IsNullOrWhiteSpace(_globalSettings.Smtp.Password)) {
                await client.AuthenticateAsync(_globalSettings.Smtp.Username, _globalSettings.Smtp.Password);
            }

            //var mailMessage = message.ToMimeMessage(_globalSettings.Smtp.From);
            if (_globalSettings.Smtp.DeliveryMethod == SmtpDeliveryMethod.Network) {
                await client.SendAsync(mm);
            }
            else {
                client.Send(mm);
            }

            await client.DisconnectAsync(true);
        }

        public async Task<MimeMessage> AssembleMessage(SecureEmailArgs args) {
            string from = GetFromAddress(args);
            if (!IsValidEmailAddress(from)) {
                string message = "Error sending email: Invalid from address.";
                _logger.LogError(message);
                throw new InvalidOperationException(message);
            }

            string sender = GetSenderAddress(args);
            if (!IsValidEmailAddress(sender)) {
                string message = "Error sending email: Invalid sender address.";
                _logger.LogError(message);
                throw new InvalidOperationException(message);
            }

            string[] recipients = ParseMailAddresses(args.RecipientEmail, "recipient").ToArray<string>();
            if (recipients.Length == 0) {
                string message = "Error sending email: Invalid recipient address(es).";
                _logger.LogError(message);
                throw new InvalidOperationException(message);
            }

            string[] cc = ParseMailAddresses(args.CcEmail, "recipient (CC)").ToArray<string>();
            string[] bcc = ParseMailAddresses(args.BccEmail, "recipient (BCC)").ToArray<string>();
            string[] replyTo = ParseMailAddresses(args.ReplyToEmail, "reply to").ToArray<string>();

            // import certificate from windows certificate store
            using (CustomSecureMimeContext myctx = new CustomSecureMimeContext(StoreLocation.CurrentUser)) {
                var formsBccEmailAddr = _configuration[FORMS_BCC_EMAIL_ADDR];
                var certSignerEmail = _configuration[CERT_SIGNER_EMAIL];

                MailboxAddress signerMailbox = new MailboxAddress(string.Empty, certSignerEmail);
                SecureMailboxAddress? signerSecureMailbox = signerMailbox as SecureMailboxAddress;

                // PROBLEM: what if there are multiple valid (current dates) certificates in the store with the same email address?
                //          a client could request their own certificate, but if we purchase it under the same email address 
                //          as the common certificate, either cert could get pulled with GetSignerCertificate()
                X509Certificate2 cert = myctx.GetSignerCertificate(signerMailbox);

                MimeMessage mm = new MimeMessage() {
                    Subject = args.Subject,
                    Date = DateTime.Now
                };

                foreach (string item in recipients) {
                    // SecureMailboxAddress seems to work just fine when the "name" is an empty string
                    mm.To.Add(new SecureMailboxAddress(string.Empty, item.Trim(), cert.Thumbprint));
                }

                foreach (string item in cc) {
                    mm.Cc.Add(new SecureMailboxAddress(string.Empty, item.Trim(), cert.Thumbprint));
                }

                foreach (var item in bcc) {
                    mm.Bcc.Add(new SecureMailboxAddress(string.Empty, item.Trim(), cert.Thumbprint));
                }

                if (args.BccFormsInbox && !string.IsNullOrWhiteSpace(formsBccEmailAddr)) {
                    mm.Bcc.Add(new SecureMailboxAddress(string.Empty, formsBccEmailAddr.Trim(), cert.Thumbprint));
                }

                if (!string.IsNullOrWhiteSpace(from)) {
                    mm.From.Add(new SecureMailboxAddress(string.Empty, from.Trim(), cert.Thumbprint));
                }

                if (!string.IsNullOrWhiteSpace(sender)) {
                    mm.Sender = new SecureMailboxAddress(string.Empty, sender.Trim(), cert.Thumbprint);
                }

                foreach (string item in replyTo) {
                    mm.ReplyTo.Add(new SecureMailboxAddress(string.Empty, item.Trim(), cert.Thumbprint));
                }

                TextFormat textFormat = TextFormat.Html;

                // use MimeEntity instead of BodyBuilder (BodyBuilder can't be passed to signing and encryption methods)
                MimeEntity bodyEntity = new TextPart(textFormat) { Text = args.Body };

                // use Multipart because it can handle attachments
                var multipart = new Multipart("mixed");
                multipart.Add(bodyEntity);

                // see http://www.mimekit.net/docs/html/Creating-Messages.htm
                if (args.Attachments != null) {
                    foreach (MimePart attachment in args.Attachments) {
                        multipart.Add(attachment);
                    }
                }

                if (args.SignEmail && await myctx.CanSignAsync(signerMailbox)) {
                    CmsSigner signer = new CmsSigner(cert);

                    // for better interoperability with other mail clients, you should use
                    // MultipartSigned.CreateAsync(SecureMimeContext, CmsSigner, MimeEntity,CancellationToken) instead as
                    // the multipart / signed format is supported among a much larger subset of mail client software.
                    multipart = await MultipartSigned.CreateAsync(myctx, signer, multipart);
                }

                mm.Body = await ApplicationPkcs7Mime.EncryptAsync(myctx, mm.To.Mailboxes, multipart);

                return mm;
            }
        }

        // if "from" is blank, use system configured email address
        private string GetFromAddress(SecureEmailArgs args) {
            string fromAddress = args.FromEmail ?? string.Empty;
            if (string.IsNullOrWhiteSpace(fromAddress)) {
                fromAddress = _contentSettings.Notifications.Email ?? string.Empty;
            }
            if (string.IsNullOrWhiteSpace(fromAddress)) {
                fromAddress = _globalSettings.Smtp?.From ?? string.Empty;
            }
            return fromAddress;
        }

        // if "sender" is blank, use "from"
        private string GetSenderAddress(SecureEmailArgs args) {
            string senderAddress = args.SenderEmail ?? string.Empty;
            if (string.IsNullOrWhiteSpace(senderAddress)) {
                senderAddress = args.FromEmail ?? string.Empty;
            }
            return senderAddress;
        }

        private bool IsValidEmailAddress(string address) {
            address = address.Trim();
            if (!string.IsNullOrWhiteSpace(address)) {
                if (address.EndsWith(".")) {
                    return false; // suggested by @TK-421
                }

                try {
                    var mailAddr = new System.Net.Mail.MailAddress(address);
                    return (mailAddr.Address == address);
                }
                catch {
                    return false;
                }
            }

            return false;
        }

        private IEnumerable<string> ParseMailAddresses(string addresses, string addressType) {
            HashSet<string> mailAddresses = new HashSet<string>();
            string msg = string.Empty;
            if (addresses == null) {
                return (IEnumerable<string>)mailAddresses;
            }
            foreach (string address in (IEnumerable<string>)addresses.Split(new char[2] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries)) {
                if (!IsValidEmailAddress(address)) {
                    msg = $"Error sending email: Invalid {addressType} address.";
                    _logger.LogWarning(msg);
                    throw new InvalidOperationException(msg);
                }
                else {
                    mailAddresses.Add(address);
                }
            }
            return (IEnumerable<string>)mailAddresses;
        }

    }
}
