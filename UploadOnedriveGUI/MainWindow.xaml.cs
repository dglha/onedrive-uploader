using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.Identity.Client;
using static System.Formats.Asn1.AsnWriter;
using Helpers;
using Microsoft.Graph;
using Microsoft.Extensions.Configuration;
using Prompt = Microsoft.Identity.Client.Prompt;
using Microsoft.Win32;
using System.IO;
using Path = System.IO.Path;
using Microsoft.Identity.Client.Extensions.Msal;
using TokenCache;
using System.Net.Http.Headers;

namespace UploadOnedriveGUI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        // GraphClient
        GraphServiceClient graphClient;

        string filePath;

        StorageCreationProperties storageProperties;

        public MainWindow()
        {
            // This hooks up the cross-platform cache into MSAL
            InitializeComponent();
            var task = Task.Run((Func<Task>)Run);
            task.Wait();

        }

        private async Task Run()
        {
            storageProperties = new StorageCreationPropertiesBuilder(CacheSettings.CacheFileName, CacheSettings.CacheDir, App.ClientId)
                .WithLinuxKeyring(
                    CacheSettings.LinuxKeyRingSchema,
                    CacheSettings.LinuxKeyRingCollection,
                    CacheSettings.LinuxKeyRingLabel,
                    CacheSettings.LinuxKeyRingAttr1,
                    CacheSettings.LinuxKeyRingAttr2)
                .WithMacKeyChain(
                    CacheSettings.KeyChainServiceName,
                    CacheSettings.KeyChainAccountName)
                .Build();
            var cacheHelper = await MsalCacheHelper.CreateAsync(storageProperties);
            cacheHelper.RegisterCache(App.PublicClientApp.UserTokenCache);

            List<string> scopes = new List<string>();
            scopes.Add("https://graph.microsoft.com/.default");

            var result = await AcquireToken(App.PublicClientApp, scopes.ToArray(), false);

            graphClient = GetGraphServiceClient(result.AccessToken);
        }

        private static async Task<AuthenticationResult> AcquireToken(IPublicClientApplication app, string[] scopes, bool useEmbaddedView)
        {
            AuthenticationResult result;
            try
            {
                var accounts = await app.GetAccountsAsync();

                // Try to acquire an access token from the cache. If an interaction is required, 
                // MsalUiRequiredException will be thrown.
                result = await app.AcquireTokenSilent(scopes, accounts.FirstOrDefault())
                            .ExecuteAsync();
            }
            catch (MsalUiRequiredException)
            {
                // Acquiring an access token interactively. MSAL will cache it so we can use AcquireTokenSilent
                // on future calls.
                result = await app.AcquireTokenInteractive(scopes)
                            .WithUseEmbeddedWebView(useEmbaddedView)
                            .ExecuteAsync();
            }

            return result;
        }

        private static GraphServiceClient GetGraphServiceClient(string accessToken)
        {
            GraphServiceClient graphServiceClient = new GraphServiceClient(
                new DelegateAuthenticationProvider(
                    async (requestMessage) =>
                    {
                        await Task.Run(() =>
                        {
                            requestMessage.Headers.Authorization = new AuthenticationHeaderValue("bearer", accessToken);
                        });
                    }));

            return graphServiceClient;
        }

        /// <summary>
        /// Call AcquireToken - to acquire a token requiring user to sign-in
        /// </summary>
        private async void CallGraphButton_Click(object sender, RoutedEventArgs e)
        {
            //graphClient = GetAuthenticatedGraphClient();
            //var user = await graphClient.Me.Request().GetAsync();

            //LoginLabel.Content = user.DisplayName.ToString();

            List<string> scopes = new List<string>();
            scopes.Add("https://graph.microsoft.com/.default");

            var result = await AcquireToken(App.PublicClientApp, scopes.ToArray(), false);

            graphClient = GetGraphServiceClient(result.AccessToken);

            var me = await graphClient.Me.Request().GetAsync();

            LoginLabel.Content = me.DisplayName.ToString();

            var check = await App.PublicClientApp.GetAccountsAsync().ConfigureAwait(false);
            if (check.Any())
            { 
                this.SignOutButton.Visibility = Visibility.Visible;
            }
        }

        private async void SignOutButton_Click(object sender, RoutedEventArgs e)
        {
            var accounts = await App.PublicClientApp.GetAccountsAsync();

            if (accounts.Any())
            {
                try
                {
                    await App.PublicClientApp.RemoveAsync(accounts.FirstOrDefault());
                    //this.ResultText.Text = "User has signed-out";
                    this.CallGraphButton.Visibility = Visibility.Visible;
                    this.SignOutButton.Visibility = Visibility.Collapsed;
                    ClearInfo();
                }
                catch (MsalException ex)
                {
                    MessageBox.Show($"Error signing-out user: {ex.Message}");
                    //ResultText.Text = $"Error signing-out user: {ex.Message}";
                }
            }
        }

        //private static IAuthenticationProvider CreateAuthenticationProvider()
        //{ 
        //    List<string> scopes = new List<string>();
        //    scopes.Add("https://graph.microsoft.com/.default");
        //    var cca = App.PublicClientApp;
        //    var username = "duongleha212001@gmail.com";
        //    var password = "Ha02012001@gmail.com";
        //    return MsalAuthenticationProvider.GetInstance(cca, scopes.ToArray());
        //}

        //private static GraphServiceClient GetAuthenticatedGraphClient()
        //{
        //    var authenticationProvider = CreateAuthenticationProvider();
        //    var graphClient = new GraphServiceClient(authenticationProvider);
        //    return graphClient;
        //}

        private void ChooseFileButton_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog();
            bool? result = openFileDialog.ShowDialog();
            if (result == true)
            {
                this.filePath = openFileDialog.FileName;
                FileNameLabel.Content = System.IO.Path.GetFileName(this.filePath);
            }
        }

        private async void UploadButton_Click(object sender, RoutedEventArgs e)
        {
            if (String.IsNullOrEmpty(this.filePath))
            {
                MessageBox.Show("Please choose file to upload");
                return;
            }
            var accounts = await App.PublicClientApp.GetAccountsAsync();

            if (!accounts.Any())
            {
                MessageBox.Show("Please sign in first");
                return;
            }
            System.IO.FileInfo fileInfo = new System.IO.FileInfo(this.filePath);
            var fileSize = fileInfo.Length / 1024f / 1024f;

            if (fileSize <= 5)
            {
                UploadButton.IsEnabled = false;
                await UploadFile();
            } else
            {
                // File larger than 5mbs
                UploadButton.IsEnabled = false;
                await UploadLargeFileAsync();
            }
            //System.Diagnostics.Debug.WriteLine($"File info: {fileInfo.Length / 1024f / 1024f} mb");
        }

        public async Task UploadFile()
        {
            FileStream fileStream = new FileStream(filePath, FileMode.Open);
            var uploadedFile = await graphClient.Me.Drive.Root
                                          .ItemWithPath(Path.GetFileName(filePath))
                                          .Content
                                          .Request()
                                          .PutAsync<DriveItem>(fileStream);
            System.Diagnostics.Debug.WriteLine("File uploaded to: " + uploadedFile.WebUrl);
            MessageBox.Show("Upload completed");
            UploadButton.IsEnabled = true;
            ClearInfo();
        }

        public async Task UploadLargeFileAsync()
        {
            using var fileStream = System.IO.File.OpenRead(filePath);

            // Use properties to specify the conflict behavior
            // in this case, replace
            var uploadProps = new DriveItemUploadableProperties
            {
                AdditionalData = new Dictionary<string, object>
                {
                    { "@microsoft.graph.conflictBehavior", "replace" }
                }
            };

            // Create the upload session
            // itemPath does not need to be a path to an existing item
            var uploadSession = await graphClient.Me.Drive.Root
                .ItemWithPath(Path.GetFileName(filePath))
                .CreateUploadSession(uploadProps)
                .Request()
                .PostAsync();

            // Max slice size must be a multiple of 320 KiB
            int maxSliceSize = 320 * 10240;
            var fileUploadTask =
                new LargeFileUploadTask<DriveItem>(uploadSession, fileStream, maxSliceSize);

            var totalLength = fileStream.Length;
            // Create a callback that is invoked after each slice is uploaded
            progressBar.Maximum = totalLength;
            IProgress<long> progress = new Progress<long>(prog => {
                System.Diagnostics.Debug.WriteLine($"Uploaded {prog} bytes of {totalLength} bytes");
                progressBar.Value = prog;
            });

            try
            {
                // Upload the file
                var uploadResult = await fileUploadTask.UploadAsync(progress);

                if (uploadResult.UploadSucceeded)
                {
                    MessageBox.Show("Upload complete", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    System.Diagnostics.Debug.WriteLine(uploadResult.UploadSucceeded ?
                    $"Upload complete, item ID: {uploadResult.ItemResponse.Id}" :
                    "Upload failed");
                    ClearInfo();
                    UploadButton.IsEnabled = true;
                }
                else
                {
                    MessageBox.Show("Upload failed", "Error", MessageBoxButton.OK);
                    UploadButton.IsEnabled = true;
                }

            }
            catch (ServiceException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error uploading: {ex.ToString()}");
            }
        }

        public void ClearInfo()
        {
            this.filePath = null;
            FileNameLabel.Content = string.Empty;
            progressBar.Value = 0;
            progressBar.Maximum = 100;
        }
    }
}