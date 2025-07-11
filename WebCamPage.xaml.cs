using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.Devices.Enumeration;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Media.Capture;
using Windows.Media.Capture.Frames;
using Windows.Media.Core;
using Windows.Media.Devices;
using Windows.Media.Playback;
using Windows.Security.Authorization.AppCapabilityAccess;
using Windows.Foundation.Metadata; // Added for ApiInformation

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace NavigationViewSample
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class WebCamPage : Page
    {
        private DeviceInformationCollection? m_deviceList;
        private MediaCapture? m_mediaCapture;
        private MediaFrameSource? m_frameSource;
        private MediaPlayer? m_mediaPlayer;
        private bool m_isPreviewing = false; // Corresponds to _WebCamPageIsPreviewing in original error

        public WebCamPage()
        {
            this.InitializeComponent(); // Ensure InitializeComponent() is called.
            // No direct fix needed for CS8618 for m_isPreviewing here, as it's a value type and defaults to false.
            // For nullable reference types declared with '?', the compiler expects them to be assigned before use.
            // Your existing '?' on m_deviceList, m_mediaCapture, m_frameSource, m_mediaPlayer already handles CS8618.
            PopulateCameraList();
        }

        private async void PopulateCameraList()
        {
            cbDeviceList.Items.Clear();

            m_deviceList = await DeviceInformation.FindAllAsync(MediaDevice.GetVideoCaptureSelector());

            if (m_deviceList.Count == 0)
            {
                tbStatus.Text = "No video capture devices found.";
                return;
            }

            foreach (var device in m_deviceList)
            {
                cbDeviceList.Items.Add(device.Name);
                // The bStartMediaCapture.IsEnabled = true; line seems fine; it enables the button if devices are found.
                bStartMediaCapture.IsEnabled = true;
            }

            // *** FIX FOR CS1501 ***
            // The original error "cbDeviceList.SelectedIndex does not contain a definition for _deivceList_SelectionChanged"
            // suggests you might have been trying to assign an event handler directly or something similar in XAML/code-behind
            // for cbDeviceList.SelectedIndex, or there was a typo referencing 'deivceList'.
            // Assuming 'cbDeviceList' is a ComboBox and you want to handle its selection:
            // Ensure the event handler for SelectionChanged is correctly wired up, usually in XAML:
            // <ComboBox x:Name="cbDeviceList" SelectionChanged="cbDeviceList_SelectionChanged"/>
            // And then implement the event handler method:
            if (cbDeviceList.Items.Count > 0)
            {
                cbDeviceList.SelectedIndex = 0; // Set a default selection if items exist
            }
        }

        // *** FIX FOR CS1501 ***
        // You likely need a SelectionChanged event handler for your ComboBox 'cbDeviceList'
        // based on the previous error message hinting at '_deivceList_SelectionChanged'.
        // This method needs to be present if you're wiring it up in XAML.
        private void cbDeviceList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // You can add logic here if something needs to happen when the selection changes.
            // For now, it can be empty if no specific action is needed besides setting the index.
            tbStatus.Text = $"Selected device: {cbDeviceList.SelectedItem}";
        }


        private async void bStartMediaCapture_Click(object sender, RoutedEventArgs e)
        {
            if (m_mediaCapture != null)
            {
                tbStatus.Text = "MediaCapture already initialized.";
                return;
            }

            // *** FIX FOR CA1416 ***
            // Supported in Windows Build 18362 and later (UniversalApiContract, Major Version 8).
            // This check prevents the AppCapability.Create calls from being attempted on older OS versions.
            if (ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 8))
            {
                if (AppCapability.Create("Webcam").CheckAccess() != AppCapabilityAccessStatus.Allowed)
                {
                    tbStatus.Text = "Camera access denied. Launching settings.";

                    bool result = await Windows.System.Launcher.LaunchUriAsync(new Uri("ms-settings:privacy-webcam"));

                    if (AppCapability.Create("Webcam").CheckAccess() != AppCapabilityAccessStatus.Allowed)
                    {
                        tbStatus.Text = "Camera access denied in privacy settings.";
                        return;
                    }
                }
            }
            else
            {
                // Handle cases where the API is not available (e.g., older Windows builds)
                tbStatus.Text = "AppCapability API not available on this Windows version. Camera access might be implicit or require manual checks.";
                // You might still proceed, but be aware of potential failures if camera access isn't granted.
                // For a robust solution, you might disable webcam functionality entirely on unsupported versions.
            }


            try
            {
                m_mediaCapture = new MediaCapture();
                var mediaCaptureInitializationSettings = new MediaCaptureInitializationSettings()
                {
                    // *** FIX FOR CS8602 ***
                    // m_deviceList can be null if PopulateCameraList() failed or returned no devices.
                    // Also, cbDeviceList.SelectedIndex could be -1 if no item is selected.
                    // Add checks to prevent dereferencing a potentially null m_deviceList or invalid index.
                    VideoDeviceId = m_deviceList != null && cbDeviceList.SelectedIndex != -1 ? m_deviceList[cbDeviceList.SelectedIndex].Id : null,
                    SharingMode = MediaCaptureSharingMode.ExclusiveControl,
                    StreamingCaptureMode = StreamingCaptureMode.Video,
                    MemoryPreference = MediaCaptureMemoryPreference.Auto
                };

                // Add a null check for VideoDeviceId before initializing
                if (string.IsNullOrEmpty(mediaCaptureInitializationSettings.VideoDeviceId))
                {
                    tbStatus.Text = "No video device selected or available.";
                    m_mediaCapture.Dispose(); // Clean up if MediaCapture was instantiated
                    m_mediaCapture = null;
                    return;
                }

                await m_mediaCapture.InitializeAsync(mediaCaptureInitializationSettings);

                tbStatus.Text = "MediaCapture initialized successfully.";

                bStartPreview.IsEnabled = true;
            }
            catch (Exception ex)
            {
                tbStatus.Text = "Initialize media capture failed: " + ex.Message;
                // Ensure m_mediaCapture is nulled out on failure to allow retries
                if (m_mediaCapture != null)
                {
                    m_mediaCapture.Dispose();
                    m_mediaCapture = null;
                }
            }
        }
        private void bStartPreview_Click(object sender, RoutedEventArgs e)
        {
            m_frameSource = null;

            // *** FIX FOR CS8602 ***
            // m_mediaCapture could be null if initialization failed.
            if (m_mediaCapture == null)
            {
                tbStatus.Text = "MediaCapture is not initialized.";
                return;
            }

            // Find preview source.
            // The preferred preview stream from a camera is defined by MediaStreamType.VideoPreview on the RGB camera (SourceKind == color).
            var previewSource = m_mediaCapture.FrameSources.FirstOrDefault(source => source.Value.Info.MediaStreamType == MediaStreamType.VideoPreview
                                                                                      && source.Value.Info.SourceKind == MediaFrameSourceKind.Color).Value;

            if (previewSource != null)
            {
                m_frameSource = previewSource;
            }
            else
            {
                var recordSource = m_mediaCapture.FrameSources.FirstOrDefault(source => source.Value.Info.MediaStreamType == MediaStreamType.VideoRecord
                                                                                          && source.Value.Info.SourceKind == MediaFrameSourceKind.Color).Value;
                if (recordSource != null)
                {
                    m_frameSource = recordSource;
                }
            }

            if (m_frameSource == null)
            {
                tbStatus.Text = "No video preview or record stream found.";
                return;
            }

            // *** FIX FOR CS8602 ***
            // Ensure m_mediaPlayer is instantiated before use.
            // If it's already instantiated, dispose of it before creating a new one to prevent resource leaks.
            if (m_mediaPlayer != null)
            {
                m_mediaPlayer.Dispose();
            }
            m_mediaPlayer = new MediaPlayer();
            m_mediaPlayer.RealTimePlayback = true;
            m_mediaPlayer.AutoPlay = false;
            m_mediaPlayer.Source = MediaSource.CreateFromMediaFrameSource(m_frameSource);
            m_mediaPlayer.MediaFailed += MediaPlayer_MediaFailed; ;

            // Set the mediaPlayer on the MediaPlayerElement
            mpePreview.SetMediaPlayer(m_mediaPlayer);

            // Start preview
            m_mediaPlayer.Play();


            tbStatus.Text = "Start preview succeeded!";
            m_isPreviewing = true;
            bStartPreview.IsEnabled = false;
            bStopPreview.IsEnabled = true;
        }

        private void MediaPlayer_MediaFailed(MediaPlayer sender, MediaPlayerFailedEventArgs args)
        {
            tbStatus.Text = "MediaPlayer error: " + args.ErrorMessage;
            // Optionally, handle cleanup or re-enable buttons here
            // e.g., bStopPreview_Click(sender, args); // Or a custom reset
        }

        private void bStopPreview_Click(object sender, RoutedEventArgs e)
        {
            // *** FIX FOR CS8602 ***
            // Ensure m_mediaPlayer is not null before calling Pause()
            if (m_mediaPlayer != null)
            {
                m_mediaPlayer.Pause();
            }
            m_isPreviewing = false;
            bStartPreview.IsEnabled = true;
            bStopPreview.IsEnabled = false;
        }

        private void bReset_Click(object sender, RoutedEventArgs e)
        {
            if (m_mediaCapture != null)
            {
                m_mediaCapture.Dispose();
                m_mediaCapture = null;
            }

            if (m_mediaPlayer != null)
            {
                m_mediaPlayer.Dispose();
                m_mediaPlayer = null;
            }

            m_frameSource = null; // No Dispose() method for MediaFrameSource itself needed here.

            bStartMediaCapture.IsEnabled = false;
            bStartPreview.IsEnabled = false;
            bStopPreview.IsEnabled = false;

            PopulateCameraList();
        }
    }
}