using Microsoft.Extensions.DependencyInjection;
using S3.Utilities.SecureEmail;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;

namespace S3.Utilities.Startup {
    public class S3UtilitiesComposer : IComposer {

        public void Compose(IUmbracoBuilder builder) {
            builder.Services.AddSingleton<IMimeSmtpService, MimeSmtpService>();
        }
    }
}
