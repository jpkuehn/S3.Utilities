using MimeKit;

namespace S3.Utilities.SecureEmail {
    public interface IMimeSmtpService {

        Task SendAsync(MimeMessage message, string emailType);

        bool CanSendRequiredEmail();

        Task<MimeMessage> AssembleMessage(SecureEmailArgs args);
    }
}
