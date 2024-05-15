
using ActiveDirectorySearcher;
using ActiveDirectorySearcher.DTOs;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace ActiveDirectoryReplication
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private bool isTaskRunning = false;
        private CancellationTokenSource? _cancellationToken;
        private DispatcherTimer timer;
        private Progress<Status> progressReporter;
        private ICollection<string> containers;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Main_Loaded(object sender, RoutedEventArgs e)
        {
            Txt_Interval.Text = "30";
            RadioBtn_All.IsChecked = true;
            Txt_Containers.Text = "CN=Users,DC=usman,DC=local;\nOU=TestOU,DC=usman,DC=local;";
            HandleReplicate_TestConnBtns();
        }



        private async void OnReplicationClick(object sender, RoutedEventArgs e)
        {
            try
            {
                isTaskRunning = false;
                containers = new List<string>();
                if (RadioBtn_Custom.IsChecked == true)
                {
                    containers = Helper.ContainerParser.Parse(Txt_Containers.Text);
                }

                SetInputsUIState(false);
                if (!long.TryParse(Txt_Interval.Text, out var interval)) //check whether it can access UI elements
                {
                    throw new InvalidOperationException("Please enter valid Interval");
                }

                // Initialize the DispatcherTimer
                timer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(interval)
                };

                timer.Tick += Timer_Tick;
                Txt_Status.Text = "Replication in progress";
                Btn_Replicate.IsEnabled = false;
                Btn_Stop.IsEnabled = true;
                ResetLogsAndResults();

                Pb_Status.IsIndeterminate = true;
                await ActiveDirectoryHelper.LoadOUReplication();
                progressReporter = new Progress<Status>(st =>
                {
                    Txt_Logs.Text += st.LogMessage;
                    Txt_Result.Text += st.ResultMessage;
                });
                _cancellationToken = new();
                timer.Start();
                Timer_Tick(timer, EventArgs.Empty);

            }
            catch (Exception ex)
            {
                HandleError(ex);
                Txt_Status.Text = "";
            }
        }

        private async void Timer_Tick(object sender, EventArgs e)
        {
            if (isTaskRunning)
            {
                PrintResultTextBoxDispatcher("Replication is already running, skipping this tick...");
                return;
            }

            try
            {
                isTaskRunning = true;

                _ = int.TryParse(Txt_Port.Text, out var port);
                InputCreds inputCreds = new(Txt_Domain.Text, Txt_Username.Text, Txt_Password.Password, port, Txt_License.Text);
                ResetLogsAndResults();
                await Task.Run(async () =>
                {
                    PrintResultTextBoxDispatcher("Start Fetching Groups");
                    await ActiveDirectoryHelper.ProcessADObjects(inputCreds, progressReporter, ObjectType.Group, containers, _cancellationToken.Token);
                    PrintResultTextBoxDispatcher("Start Fetching Users");
                    await ActiveDirectoryHelper.ProcessADObjects(inputCreds, progressReporter, ObjectType.User, containers, _cancellationToken.Token);
                    PrintResultTextBoxDispatcher("Finished Replication");
                    DumpLogsWithDispatcher();
                });
                _cancellationToken?.Token.ThrowIfCancellationRequested();
            }
            catch (Exception ex)
            {
                StopTimer();
                Pb_Status.Value = 0;
                Btn_Replicate.IsEnabled = true;
                Btn_Stop.IsEnabled = false;
                Pb_Status.IsIndeterminate = false;
                await ActiveDirectoryHelper.WriteOUReplication();
                if (ex is OperationCanceledException)
                {
                    MessageBox.Show("Search has been cancelled.", "Search Cancelled", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                else
                {
                    HandleError(ex);
                }
                Txt_Status.Text = "";
                SetInputsUIState(true);               
            }
            finally
            {
                isTaskRunning = false;
            }
        }

        private void OnStopReplicationClick(object sender, RoutedEventArgs e)
        {
            if (_cancellationToken is not null)
            {
                Btn_Stop.IsEnabled = false;
                _cancellationToken.Cancel();
                Txt_Status.Text = "Stopping Replication..";
            }
        }

        private void RadioButton_Checked(object sender, RoutedEventArgs e)
        {
            // Cast sender to RadioButton to determine which radio button is checked
            RadioButton radioButton = (RadioButton)sender;

            // Update the resultTextBlock based on which radio button is checked
            if (radioButton != null && radioButton.IsChecked == true)
            {
                if (radioButton == RadioBtn_All)
                {
                    Txt_Containers.IsEnabled = false;
                    //resultTextBlock = "Option 1 is selected.";
                }
                else if (radioButton == RadioBtn_Custom)
                {
                    Txt_Containers.IsEnabled = true;
                }
            }
        }
        private void DumpLogsWithDispatcher()
        {
            Application.Current.Dispatcher.Invoke(async () =>
            {
                try
                {
                    CustomLogger customLogger = new CustomLogger("logs.txt", "");
                    var txt = Txt_Result.Text + Environment.NewLine + "TxtLogs: " + Environment.NewLine + Txt_Logs.Text;
                    await Task.Run(() => customLogger.WriteInfo(txt));
                }
                catch (Exception ex)
                {
                    Txt_Result.Text += $"Error {ex.Message}In writing logs{Environment.NewLine}";
                }
            });
        }

        private void StopTimer() //can be called multiple thread
        {
            timer.Stop();
            timer.Tick -= Timer_Tick;
            timer = null;
            //try statement  timer.Tick -= Timer_Tick; now it shouldn't give exception
        }


        private async void OnTestConnectionClick(object sender, RoutedEventArgs e)
        {
            try
            {
                Btn_TestConnection.IsEnabled = false;
                int.TryParse(Txt_Port.Text, out int port);
                using (await ActiveDirectoryHelper.GetRootEntry(new(Txt_Domain.Text, Txt_Username.Text, Txt_Password.Password, port, Txt_License.Text)))
                {
                    // empty (just for disposing the entry created for testing the connection)
                }

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

        private void PrintResultTextBoxDispatcher(string message)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                Txt_Result.Text += message + "\n";
            });
        }

        private void TxtDomainOrLicenseOrIntervalChanged(object sender, TextChangedEventArgs e)
        {
            HandleReplicate_TestConnBtns();
        }

        private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            Regex regex = new Regex("[^0-9]+");
            e.Handled = regex.IsMatch(e.Text);
        }
        #region helping methods
        private void HandleReplicate_TestConnBtns()
        {
            Btn_Replicate.IsEnabled = !string.IsNullOrWhiteSpace(Txt_Domain.Text) && !string.IsNullOrWhiteSpace(Txt_License.Text) && !string.IsNullOrWhiteSpace(Txt_Interval.Text);
            Btn_TestConnection.IsEnabled = Btn_Replicate.IsEnabled;
        }
        private void HandleError(Exception ex)
        {
            MessageBox.Show(ex.Message, ex.GetType().FullName, MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private void ResetLogsAndResults()
        {
            Txt_Result.Text = "";
            Txt_Logs.Text = "";
        }

        private void SetInputsUIState(bool enable)
        {
            Txt_Domain.IsEnabled = enable;
            Txt_Port.IsEnabled = enable;
            Txt_Username.IsEnabled = enable;
            Txt_Password.IsEnabled = enable;
            Txt_Interval.IsEnabled = enable;
            Txt_License.IsEnabled = enable;
            Btn_TestConnection.IsEnabled = enable;
            RadioBtn_All.IsEnabled = enable;
            RadioBtn_Custom.IsEnabled = enable;
            Txt_Containers.IsEnabled = enable;
            if(enable && RadioBtn_All.IsChecked==true)
            {
                Txt_Containers.IsEnabled = false;
            }
            else
                Txt_Containers.IsEnabled = enable;

        }
        #endregion
    }
}
