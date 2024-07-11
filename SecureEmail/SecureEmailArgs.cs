using MimeKit;

namespace S3.Utilities.SecureEmail {

    // Summary: Defines arguments for sending an email used in S3.Workflows.Forms.ISecureEmailService
    public class SecureEmailArgs {
        // Summary: Gets or sets the email address(es) of the recipient(s).
        // Remarks: If more than one address, string should be comma or semi-colon separated.
        public string RecipientEmail { get; set; } = string.Empty;

        // Summary: Gets or sets the email address(es) of the recipient(s) in CC.
        // Remarks: If more than one address, string should be comma or semi-colon separated.
        public string CcEmail { get; set; } = string.Empty;

        // Summary: Gets or sets the email address(es) of the recipient(s) in BC.
        // Remarks: If more than one address, string should be comma or semi-colon separated.
        public string BccEmail { get; set; } = string.Empty;

        // Summary: Gets or sets a value indicating whether the Forms inbox should be bcc'd
        public bool BccFormsInbox { get; set; } = true;

        // Summary: Gets or sets the email address of the author (can be different than sender).
        public string FromEmail { get; set; } = string.Empty;

        // Summary: Gets or sets the email address of the sender.
        public string SenderEmail { get; set; } = string.Empty;

        // Summary: Gets or sets the email address(es) of the reply to addresse(s).
        // Remarks: If more than one address, string should be comma or semi-colon separated.
        public string ReplyToEmail { get; set; } = string.Empty;

        // Summary: Gets or sets the subject of the email message.
        public string Subject { get; set; } = string.Empty;

        // Summary: Gets or sets the body of the email message.
        public string Body { get; set; } = string.Empty;

        // Summary: Gets or sets a collection of attachments to send along with the email message.
        public List<MimePart> Attachments { get; set; } = new List<MimePart>();

        // Summary: Gets or sets a value indicating whether the email should be signed with the S/MIME certificate
        public bool SignEmail { get; set; } = true;

    }
}