using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Helpers;
using Microsoft.Extensions.Configuration;
using Microsoft.Graph;
using Microsoft.Identity.Client;

namespace OnedriveUpload
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private async void Form1_Load(object sender, EventArgs e)
        {
            var client = GetAuthenticatedGraphClient();

            var profileResponse = await client.Me.Request().GetAsync();
            Console.WriteLine("Hello " + profileResponse.DisplayName);

            //System.Diagnostics.Debug.WriteLine("Hello " + profileResponse.DisplayName);
        }

        private static IAuthenticationProvider CreateAuthenticationProvider()
        {
            var clientId = "";
            var authority = $"https://login.microsoftonline.com/consumers/{""}/v2.0";

            List<string> scopes = new List<string>();
            scopes.Add("https://graph.microsoft.com/.default");

            var cca = PublicClientApplicationBuilder.Create(clientId)
                                          .WithAuthority(authority)
                                          .WithDefaultRedirectUri()
                                          .Build();
            return MsalAuthenticationProvider.GetInstance(cca, scopes.ToArray());
        }

        private static GraphServiceClient GetAuthenticatedGraphClient()
        {
            var authenticationProvider = CreateAuthenticationProvider();
            var graphClient = new GraphServiceClient(authenticationProvider);
            return graphClient;
        }
    }
}
