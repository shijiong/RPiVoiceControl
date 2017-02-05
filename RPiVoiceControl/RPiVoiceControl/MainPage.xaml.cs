using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

using Windows.Devices.Gpio;
using Windows.Media.SpeechRecognition;
using Windows.Media.SpeechSynthesis;
using Windows.Storage;
using Windows.ApplicationModel;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace RPiVoiceControl
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        #region Speech Commands You Can Say
        //==================        
        // hi Jack
        // Turn On/Off Bedroom Light
        // Turn On/Off kITCHEN Light     
        //================== 
        #endregion

        #region 
        // Grammer File
        private const string SRGS_FILE = "Grammar.xml";
        // Speech Recognizer
        private SpeechRecognizer recognizer;
        // Tag TARGET
        private const string TAG_TARGET = "location";
        // Tag CMD
        private const string TAG_CMD = "cmd";
        // Tag Device
        private const string TAG_DEVICE = "device";
        #endregion

        #region BedRoom
        private const int BedRoomLED_PINNumber = 5;
        private GpioPin BedRoomLED_GpioPin;
        private GpioPinValue BedRoomLED_GpioPinValue;
        private DispatcherTimer bedRoomTimer;
        #endregion

        #region kITCHEN
        private const int kITCHENLED_PINNumber = 6;
        private GpioPin kITCHENLED_GpioPin;
        private GpioPinValue kITCHENLED_GpioPinValue;
        private DispatcherTimer kITCHENTimer;
        #endregion

        private SolidColorBrush redBrush = new SolidColorBrush(Windows.UI.Colors.Red);
        private SolidColorBrush grayBrush = new SolidColorBrush(Windows.UI.Colors.LightGray);

        public MainPage()
        {
            this.InitializeComponent();
            Unloaded += MainPage_Unloaded;

            // Initialize Recognizer
            initializeSpeechRecognizer();

            InitBedRoomGPIO();
            InitKITCHENGPIO();

            bedRoomTimer = new DispatcherTimer();
            bedRoomTimer.Interval = TimeSpan.FromMilliseconds(500);
            bedRoomTimer.Tick += BedRoomTimer_Tick;

            kITCHENTimer = new DispatcherTimer();
            kITCHENTimer.Interval = TimeSpan.FromMilliseconds(500);
            kITCHENTimer.Tick += KITCHENTimer_Tick;
        }

        // Initialize Speech Recognizer and start async recognition
        private async void initializeSpeechRecognizer()
        {
            // Initialize recognizer
            recognizer = new SpeechRecognizer();

            #region Create Events
            // Set event handlers
            recognizer.StateChanged += RecognizerStateChanged;
            recognizer.ContinuousRecognitionSession.ResultGenerated += RecognizerResultGenerated;
            #endregion

            #region Load Grammar
            // Load Grammer file constraint
            string fileName = String.Format(SRGS_FILE);
            StorageFile grammarContentFile = await Package.Current.InstalledLocation.GetFileAsync(fileName);
            SpeechRecognitionGrammarFileConstraint grammarConstraint = new SpeechRecognitionGrammarFileConstraint(grammarContentFile);

            // Add to grammer constraint
            recognizer.Constraints.Add(grammarConstraint);
            #endregion

            #region Compile grammer
            SpeechRecognitionCompilationResult compilationResult = await recognizer.CompileConstraintsAsync();
            Debug.WriteLine("Status: " + compilationResult.Status.ToString());

            // If successful, display the recognition result.
            if (compilationResult.Status == SpeechRecognitionResultStatus.Success)
            {
                Debug.WriteLine("Result: " + compilationResult.ToString());

                await recognizer.ContinuousRecognitionSession.StartAsync();
            }
            else
            {
                Debug.WriteLine("Status: " + compilationResult.Status);
            }
            #endregion
        }

        // Recognizer generated results
        private async void RecognizerResultGenerated(SpeechContinuousRecognitionSession session, SpeechContinuousRecognitionResultGeneratedEventArgs args)
        {
            // Output debug strings
            Debug.WriteLine(args.Result.Status);
            Debug.WriteLine(args.Result.Text);
            int count = args.Result.SemanticInterpretation.Properties.Count;
            Debug.WriteLine("Count: " + count);
            Debug.WriteLine("Tag: " + args.Result.Constraint.Tag);

            // Check for different tags and initialize the variables
            String location = args.Result.SemanticInterpretation.Properties.ContainsKey(TAG_TARGET) ?
                            args.Result.SemanticInterpretation.Properties[TAG_TARGET][0].ToString() :
                            "";

            String cmd = args.Result.SemanticInterpretation.Properties.ContainsKey(TAG_CMD) ?
                            args.Result.SemanticInterpretation.Properties[TAG_CMD][0].ToString() :
                            "";

            String device = args.Result.SemanticInterpretation.Properties.ContainsKey(TAG_DEVICE) ?
                            args.Result.SemanticInterpretation.Properties[TAG_DEVICE][0].ToString() :
                            "";

            Debug.WriteLine("Target: " + location + ", Command: " + cmd + ", Device: " + device);
            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                VoiceStatus.Text= "Target: " + location + ", Command: " + cmd + ", Device: " + device;
            });

            #region
            switch (device)
            {
                case "hiActivationCMD"://Activate device                   
                    SaySomthing("hiActivationCMD", "On");
                    break;

                case "LIGHT":
                    LightControl(cmd, location);
                    break;

                default:
                    break;
            }
            #endregion
        }

        // Recognizer state changed
        private async void RecognizerStateChanged(SpeechRecognizer sender, SpeechRecognizerStateChangedEventArgs args)
        {
            Debug.WriteLine("Speech recognizer state: " + args.State.ToString());
            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                VoiceStatus.Text = "Speech recognizer state: " + args.State.ToString();
            });
        }

        private async void SaySomthing(string myDevice, string State, int speechCharacterVoice = 0)
        {
            if (myDevice == "hiActivationCMD")
                PlayVoice($"Hi Jack What can i do for you");
            else
                PlayVoice($"OK Jack {myDevice}  {State}", speechCharacterVoice);
            Debug.WriteLine($"OK -> ===== {myDevice} --- {State} =======");
            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                VoiceStatus.Text = $"OK -> ===== {myDevice} --- {State} =======";
            });
        }

        private async void PlayVoice(string tTS, int voice = 0)
        {
            //await Window.Current.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () => {
            //=====================
            // The media object for controlling and playing audio.           
            // The object for controlling the speech synthesis engine (voice).          
            SpeechSynthesizer speechSynthesizer = new SpeechSynthesizer();
            //speechSynthesizer.Voice = SpeechSynthesizer.DefaultVoice;
            speechSynthesizer.Voice = SpeechSynthesizer.AllVoices[voice];//0,4,8,12

            // Generate the audio stream from plain text.
            SpeechSynthesisStream spokenStream = await speechSynthesizer.SynthesizeTextToStreamAsync(tTS);

            await mediaElement.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, new Windows.UI.Core.DispatchedHandler(() =>
            {
                mediaElement.SetSource(spokenStream, spokenStream.ContentType);
                mediaElement.Play();
            }));

        }

        private void FanControl(string command)
        {
            if (command == "ON")
            {

            }
            else if (command == "Off")
            {

            }

            SaySomthing("Fan", command);
        }

        private async void LightControl(string command, string target)
        {
            if (target == "Bedroom")
            {
                if (command == "ON")
                {
                    if (BedRoomLED_GpioPin != null)
                    {
                        await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                        {
                            bedRoomTimer.Start();
                        }
                     );

                    }
                }
                else if (command == "OFF")
                {

                    await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                    {
                        bedRoomTimer.Stop();
                        if (BedRoomLED_GpioPinValue == GpioPinValue.Low)
                        {
                            BedRoomLED_GpioPinValue = GpioPinValue.High;
                            BedRoomLED_GpioPin.Write(BedRoomLED_GpioPinValue);
                            bedroomLED.Fill = grayBrush;
                        }
                    }
                    );
                }
            }
            else if (target == "kitchen")
            {
                if (command == "ON")
                {
                    if (kITCHENLED_GpioPin != null)
                    {
                        await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                        {
                            kITCHENTimer.Start();
                        }
                     );

                    }
                }
                else if (command == "OFF")
                {
                    await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                    {
                        kITCHENTimer.Stop();
                        if (kITCHENLED_GpioPinValue == GpioPinValue.Low)
                        {
                            kITCHENLED_GpioPinValue = GpioPinValue.High;
                            kITCHENLED_GpioPin.Write(kITCHENLED_GpioPinValue);
                            kitchenroomLED.Fill = grayBrush;
                        }
                    }
                    );
                }
            }

            SaySomthing($"{target} light", command);
        }

        private void InitBedRoomGPIO()
        {
            var gpio = GpioController.GetDefault();

            // Show an error if there is no GPIO controller
            if (gpio == null)
            {
                BedRoomLED_GpioPin = null;
                GpioStatus.Text = "There is no GPIO controller on this device.";
                return;
            }

            BedRoomLED_GpioPin = gpio.OpenPin(BedRoomLED_PINNumber);
            BedRoomLED_GpioPinValue = GpioPinValue.High;
            BedRoomLED_GpioPin.Write(BedRoomLED_GpioPinValue);
            BedRoomLED_GpioPin.SetDriveMode(GpioPinDriveMode.Output);

            GpioStatus.Text = "GPIO pin initialized correctly.";

        }

        private void InitKITCHENGPIO()
        {
            var gpio = GpioController.GetDefault();

            // Show an error if there is no GPIO controller
            if (gpio == null)
            {
                kITCHENLED_GpioPin = null;
                GpioStatus.Text = "There is no GPIO controller on this device.";
                return;
            }

            kITCHENLED_GpioPin = gpio.OpenPin(kITCHENLED_PINNumber);
            kITCHENLED_GpioPinValue = GpioPinValue.High;
            kITCHENLED_GpioPin.Write(kITCHENLED_GpioPinValue);
            kITCHENLED_GpioPin.SetDriveMode(GpioPinDriveMode.Output);

            GpioStatus.Text = "GPIO pin initialized correctly.";

        }

        private void BedRoomTimer_Tick(object sender, object e)
        {
            if (BedRoomLED_GpioPinValue == GpioPinValue.High)
            {
                BedRoomLED_GpioPinValue = GpioPinValue.Low;
                BedRoomLED_GpioPin.Write(BedRoomLED_GpioPinValue);
                bedroomLED.Fill = redBrush;
            }
            else
            {
                BedRoomLED_GpioPinValue = GpioPinValue.High;
                BedRoomLED_GpioPin.Write(BedRoomLED_GpioPinValue);
                bedroomLED.Fill = grayBrush;
            }
        }

        private void KITCHENTimer_Tick(object sender, object e)
        {
            if (kITCHENLED_GpioPinValue == GpioPinValue.High)
            {
                kITCHENLED_GpioPinValue = GpioPinValue.Low;
                kITCHENLED_GpioPin.Write(kITCHENLED_GpioPinValue);
                kitchenroomLED.Fill = redBrush;
            }
            else
            {
                kITCHENLED_GpioPinValue = GpioPinValue.High;
                kITCHENLED_GpioPin.Write(kITCHENLED_GpioPinValue);
                kitchenroomLED.Fill = grayBrush;
            }
        }

        // Release resources, stop recognizer etc...
        private async void MainPage_Unloaded(object sender, object args)
        {
            // Stop recognizing
            await recognizer.ContinuousRecognitionSession.StopAsync();         
            recognizer.Dispose();
            recognizer = null;

        }
    }
}
