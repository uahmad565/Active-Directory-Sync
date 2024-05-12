
using ActiveDirectorySearcher;
using ActiveDirectorySearcher.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
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

namespace ActiveDirectoryReplication
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private CancellationTokenSource? _cancellationToken;
        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Main_Loaded(object sender, RoutedEventArgs e)
        {
            Btn_Replicate.IsEnabled = !string.IsNullOrWhiteSpace(Txt_Domain.Text);
            Btn_TestConnection.IsEnabled = !string.IsNullOrWhiteSpace(Txt_Domain.Text);
        }
        private async void OnReplicationClick(object sender, RoutedEventArgs e)
        {
            try
            {
                Btn_Replicate.IsEnabled = false;
                Btn_Stop.IsEnabled = true;
                Txt_Logs.Text = "";
                Txt_Result.Text = "";
                Pb_Status.IsIndeterminate = true;
                Progress<Status> progressReporter = new Progress<Status>(st =>
                {
                    Txt_Logs.Text += st.LogMessage;
                    Txt_Result.Text += st.ResultMessage;
                });

                _cancellationToken = new();
                int.TryParse(Txt_Port.Text, out var port);
                InputCreds inputCreds = new(Txt_Domain.Text, Txt_Username.Text, Txt_Password.Password, port);
                var task = Task.Run(async () =>
                {
                    PrintResultTextBox("Start Fetching Groups");
                    await ActiveDirectoryHelper.GetADObjects(2,inputCreds, progressReporter, ObjectType.Group, _cancellationToken.Token);
                    PrintResultTextBox("Start Fetching Users");
                    await ActiveDirectoryHelper.GetADObjects(2, inputCreds, progressReporter, ObjectType.User, _cancellationToken.Token);
                    PrintResultTextBox("Finished Replication");
                });

                await task;

            }
            catch (OperationCanceledException)
            {
                MessageBox.Show("Search has been cancelled.", "Search Cancelled", MessageBoxButton.OK, MessageBoxImage.Warning);
                Pb_Status.Value = 0;
            }
            catch (Exception ex)
            {
                HandleError(ex);
            }
            finally
            {
                Btn_Replicate.IsEnabled = true;
                Btn_Stop.IsEnabled = false;
                Pb_Status.IsIndeterminate = false;
                Pb_Status.Value = 100f;
            }
        }

        private void PrintResultTextBox(string message)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                Txt_Result.Text += message + "\n";
            });

        }
        private void OnStopReplicationClick(object sender, RoutedEventArgs e)
        {
            if (_cancellationToken is not null)
            {
                Btn_Stop.IsEnabled = false;
                _cancellationToken.Cancel();
            }
        }

        private void OnTestConnectionClick(object sender, RoutedEventArgs e)
        {
            try
            {
                Btn_TestConnection.IsEnabled = false;
                int.TryParse(Txt_Port.Text, out int port);
                ActiveDirectoryHelper.TestConnection(new(Txt_Domain.Text, Txt_Username.Text, Txt_Password.Password, port));
                MessageBox.Show("Connection established successfully.", "Test Connection", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                HandleError(ex);
            }
            finally
            {
                Btn_TestConnection.IsEnabled = true;
            }
        }

        private void TxtDomainChanged(object sender, TextChangedEventArgs e)
        {
            Btn_Replicate.IsEnabled = !string.IsNullOrWhiteSpace(Txt_Domain.Text);
            Btn_TestConnection.IsEnabled = !string.IsNullOrWhiteSpace(Txt_Domain.Text);
        }

        private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            Regex regex = new Regex("[^0-9]+");
            e.Handled = regex.IsMatch(e.Text);
        }
        #region helping methods
        private void HandleError(Exception ex)
        {
            MessageBox.Show(ex.Message, ex.GetType().FullName, MessageBoxButton.OK, MessageBoxImage.Error);
        }
        #endregion
    }
}
