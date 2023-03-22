using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Extensions.Msal;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using TokenCache;

namespace UploadOnedriveGUI
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        static App()
        {
            _clientApp = PublicClientApplicationBuilder.Create(ClientId)
                .WithAuthority($"https://login.microsoftonline.com/consumers/{tenantId}/v2.0")
                .WithDefaultRedirectUri()
                .Build();

        }
        public static string ClientId = "Put your client id here" ;

        private static string tenantId = "Put your tenant id here";

        private static IPublicClientApplication _clientApp;

        public static IPublicClientApplication PublicClientApp { get { return _clientApp; } }

    }

}