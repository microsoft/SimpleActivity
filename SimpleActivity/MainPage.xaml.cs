/*	
The MIT License (MIT)
Copyright (c) 2015 Microsoft

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. 
 */

using Lumia.Sense;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Windows.UI;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Shapes;

namespace SimpleActivity
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        #region Constant definitions
        /// <summary>
        /// Activity color mappings
        /// </summary>
        private static readonly Dictionary<Activity, Color> ACTIVITY_COLORS = new Dictionary<Activity, Color>
        {
            { Activity.Idle, Colors.Gray },
            { Activity.Stationary, Colors.LightGray },
            { Activity.Moving, Colors.Orange },
            { Activity.Walking, Colors.Green },
            { Activity.Running, Colors.Red },
            { Activity.Biking, Colors.Yellow },
            { Activity.MovingInVehicle, Colors.Magenta }, 
            { Activity.Unknown, Colors.SkyBlue }
        };

        /// <summary>
        /// Zoom levels
        /// </summary>
        private static readonly double[] ZOOM_LEVELS =
        {
            1.0,
            60 / 5,
            60,
        };

        /// <summary>
        /// Base draw scale (activity bar height = zoom factor * activity length in seconds * base scale)
        /// </summary>
        private const double BASE_DRAW_SCALE = 1;

        /// <summary>
        /// Activity bar width
        /// </summary>
        private const double ACTIVITY_BAR_WIDTH = 200;
        #endregion

        #region Private members
        /// <summary>
        /// Activity monitor instance
        /// </summary>
        ActivityMonitor _activityMonitor = null;

        /// <summary>
        /// Step counter instance
        /// </summary>
        StepCounter _stepCounter = null;

        /// <summary>
        /// Current zoom level
        /// </summary>
        private int _currentZoomLevel = 0;

        /// <summary>
        /// Current date for moving between next and previous days
        /// </summary>
        private DateTime _iCurrentDate = DateTime.Today;

        /// <summary>
        /// check to see launching finished or not
        /// </summary>
        private bool iLaunched = false;
        #endregion

        /// <summary>
        /// Constructor
        /// </summary>
        public MainPage()
        {
            this.InitializeComponent();
            this.NavigationCacheMode = NavigationCacheMode.Required;

            Window.Current.VisibilityChanged += async ( oo, ee ) =>
            {
                if( !ee.Visible && _activityMonitor != null )
                {
                    await CallSenseApiAsync( async () =>
                    {
                        await _activityMonitor.DeactivateAsync();
                    } );
                    await CallSenseApiAsync( async () =>
                    {
                        await _stepCounter.DeactivateAsync();
                    } );
                }
                else if( _activityMonitor != null )
                {
                    await CallSenseApiAsync( async () =>
                    {
                        await _activityMonitor.ActivateAsync();
                    } );
                    await CallSenseApiAsync( async () =>
                    {
                        await _stepCounter.ActivateAsync();
                    } );

                    // Refresh screen
                    await UpdateScreenAsync();
                }
            };
        }

        /// <summary>
        /// Initializes sensors
        /// </summary>
        /// <returns>Asynchronous task</returns>
        private async Task Initialize()
        {
            if( !( await ActivityMonitor.IsSupportedAsync() ) )
            {
                MessageDialog dlg = new MessageDialog( "Unfortunately this device does not support activities." );
                await dlg.ShowAsync();
                Application.Current.Exit();
            }
            else
            {
                uint apiSet = await SenseHelper.GetSupportedApiSetAsync();
                MotionDataSettings settings = await SenseHelper.GetSettingsAsync();

                // devices with old sensorCore SDK service
                if( settings.Version < 2 && !settings.LocationEnabled )
                {
                    MessageDialog dlg = new MessageDialog( "In order to recognize activities you need to enable location in system settings. Do you want to open settings now? if no, applicatoin will exit", "Information" );
                    dlg.Commands.Add( new UICommand( "Yes", new UICommandInvokedHandler( async ( cmd ) => await SenseHelper.LaunchLocationSettingsAsync() ) ) );
                    dlg.Commands.Add( new UICommand( "No", new UICommandInvokedHandler( ( cmd ) =>
                    {
                        Application.Current.Exit();
                    } ) ) );
                    await dlg.ShowAsync();
                }

                if( !settings.PlacesVisited )
                {
                    MessageDialog dlg = null;
                    if( settings.Version < 2 )
                    {
                        //device which has old motion data settings.
                        //this is equal to motion data settings on/off in old system settings(SDK1.0 based)
                        dlg = new MessageDialog( "In order to recognize activities you need to enable Motion data in Motion data settings. Do you want to open settings now? if no, application will exit", "Information" );
                        dlg.Commands.Add( new UICommand( "No", new UICommandInvokedHandler( ( cmd ) =>
                        {
                            Application.Current.Exit();
                        } ) ) );
                    }
                    else
                    {
                        dlg = new MessageDialog( "In order to recognize activities you need to 'enable Places visited' and 'DataQuality to detailed' in Motion data settings. Do you want to open settings now? ", "Information" );
                        dlg.Commands.Add( new UICommand( "No" ) );
                    }
                    dlg.Commands.Add( new UICommand( "Yes", new UICommandInvokedHandler( async ( cmd ) => await SenseHelper.LaunchSenseSettingsAsync() ) ) );
                    await dlg.ShowAsync();
                }
                else if( apiSet >= 3 && settings.DataQuality == DataCollectionQuality.Basic )
                {
                    MessageDialog dlg = new MessageDialog( "In order to recognize biking you need to enable detailed data collection in Motion data settings. Do you want to open settings now?", "Information" );
                    dlg.Commands.Add( new UICommand( "Yes", new UICommandInvokedHandler( async ( cmd ) => await SenseHelper.LaunchSenseSettingsAsync() ) ) );
                    dlg.Commands.Add( new UICommand( "No" ) );
                    await dlg.ShowAsync();
                }
            }

            //in case if the device has old software(earlier than SDK1.1) or system settings changed after sometime, CallSenseApiAsync() method handles the system settings prompts.
            if( _activityMonitor == null )
            {
                if( !await CallSenseApiAsync( async () =>
                {
                    _activityMonitor = await ActivityMonitor.GetDefaultAsync();
                } ) )
                {
                    Application.Current.Exit();
                }
            }
            await UpdateScreenAsync();
        }

        /// <summary>
        /// Invoked when this page is about to be displayed in a Frame.
        /// </summary>
        /// <param name="e">Event data that describes how this page was reached.
        /// This parameter is typically used to configure the page.</param>
        protected override async void OnNavigatedTo( NavigationEventArgs e )
        {
            // Make sure the sensors are instantiated
            if( !iLaunched )
            {
                iLaunched = true;
                await Initialize();
            }
        }

        /// <summary>
        /// Updates visualization
        /// </summary>
        private async Task UpdateScreenAsync()
        {
            // clear visual
            ActivityPanel.Children.Clear();
            TimePanel.Children.Clear();

            //update labels
            ZoomLabel.Text = "" + ZOOM_LEVELS[ _currentZoomLevel ] + " pixel(s)/minute";
            DateLabel.Text = _iCurrentDate.ToString( "dd/MM/yyyy", CultureInfo.InvariantCulture );
            //update date selected
            DateTime endDate = _iCurrentDate.AddDays( 1 );
            if( _iCurrentDate.Date == DateTime.Now.Date )
            {
                endDate = DateTime.Now;
            }
            // Add time labels and steps(walking+running) count for each hour
            for( int i = 0; i < 24; i++ )
            {
                Grid timeBlock = new Grid();
                StepCount stepCount = null;
                // getting steps count
                try
                {
                    DateTime fromDate = _iCurrentDate + TimeSpan.FromHours( i );
                    TimeSpan queryLength = TimeSpan.FromHours( 1 );
                    if( ( fromDate + queryLength ) > endDate )
                    {
                        queryLength = endDate - fromDate;
                    }
                    stepCount = await _stepCounter.GetStepCountForRangeAsync( fromDate, queryLength );
                }
                catch( Exception )
                {
                }

                //updating steps count for each hour in visualizer
                TextBlock label = new TextBlock();
                label.Height = ZOOM_LEVELS[ _currentZoomLevel ] * 60.0 * BASE_DRAW_SCALE;
                label.FontSize = 14.0;
                if( stepCount != null )
                {
                    label.Text = String.Format(
                        "{0:00}:00\n{1}/{2}",
                        i,
                        stepCount.WalkingStepCount,
                        stepCount.RunningStepCount
                        );
                }
                else
                {
                    label.Text = String.Format( "{0:00}:00", i );
                }
                label.VerticalAlignment = Windows.UI.Xaml.VerticalAlignment.Top;
                timeBlock.Children.Add( label );
                //creating time(hour) intervel blocks
                Rectangle divider = new Rectangle();
                divider.Width = 200.0;
                divider.Height = 0.5;
                divider.Fill = new SolidColorBrush( Colors.Gray );
                divider.VerticalAlignment = Windows.UI.Xaml.VerticalAlignment.Top;
                timeBlock.Children.Add( divider );

                TimePanel.Children.Add( timeBlock );
            }

            // Add activities for each hour in a day
            IList<ActivityMonitorReading> activities = null;
            if( await CallSenseApiAsync( async () =>
            {
                activities = await _activityMonitor.GetActivityHistoryAsync( _iCurrentDate, endDate - _iCurrentDate );
            } ) )
            {
                if( activities.Count >= 2 )
                {
                    ActivityMonitorReading previousReading = activities[ 0 ];
                    // if first activity started few minutes after the day started then Add filler if needed
                    if( previousReading.Timestamp > _iCurrentDate )
                    {
                        AppendActivityBarBlock( Colors.Transparent, previousReading.Timestamp - _iCurrentDate );
                    }
                    // Add activities
                    for( int i = 1; i < activities.Count; i++ )
                    {
                        ActivityMonitorReading reading = activities[ i ];
                        TimeSpan activityLength = reading.Timestamp - previousReading.Timestamp;
                        // if first activity started before the day started then cut off any excess
                        if( previousReading.Timestamp < _iCurrentDate )
                            activityLength -= ( _iCurrentDate - previousReading.Timestamp );

                        AppendActivityBarBlock( ACTIVITY_COLORS[ previousReading.Mode ], activityLength );
                        previousReading = reading;
                    }
                    // Show also current activity
                    AppendActivityBarBlock( ACTIVITY_COLORS[ previousReading.Mode ], endDate - previousReading.Timestamp );
                }
                // Scroll to present/current time
                ActivityScroller.UpdateLayout();
                double scrollTo = ( ZOOM_LEVELS[ _currentZoomLevel ] * ( endDate - _iCurrentDate ).TotalMinutes * BASE_DRAW_SCALE ) - 400.0;
                if( scrollTo < 0.0 )
                    scrollTo = 0.0;
                ActivityScroller.ChangeView( null, scrollTo, null );
            }
            else
            {
                MessageDialog dlg = new MessageDialog( "Failed to fetch activities" );
                await dlg.ShowAsync();
            }
        }

        /// <summary>
        /// Renders activity graph into a bitmap
        /// </summary>
        /// <returns>Rendered bitmap</returns>
        private async Task<RenderTargetBitmap> RenderActivityBitmapAsync()
        {
            RenderTargetBitmap bmp = new RenderTargetBitmap();
            await bmp.RenderAsync( ActivityGrid );
            return bmp;
        }

        /// <summary>
        /// Appends activity bar block into UI
        /// </summary>
        /// <param name="color">Bar block color</param>
        /// <param name="activityLength">Activity length</param>
        private void AppendActivityBarBlock( Color color, TimeSpan activityLength )
        {
            Rectangle rect = new Rectangle();
            rect.Width = ACTIVITY_BAR_WIDTH;
            rect.Height = ZOOM_LEVELS[ _currentZoomLevel ] * activityLength.TotalMinutes * BASE_DRAW_SCALE;
            rect.Fill = new SolidColorBrush( color );
            ActivityPanel.Children.Add( rect );
        }

        /// <summary>
        /// Get the activity for previous day.
        /// </summary>
        /// <param name="sender">The control that the action is for.</param>
        /// <param name="e">Parameter that contains the event data.</param>
        private async void GoToPreviousDay( object sender, RoutedEventArgs e )
        {
            if( ( DateTime.Now.Date - _iCurrentDate ) < TimeSpan.FromDays( 6 ) )
            {
                _iCurrentDate = _iCurrentDate.AddDays( -1 );
                await UpdateScreenAsync();
            }
            else
            {
                MessageDialog dialog = new MessageDialog( "This application get only seven days of activities.", "Activities" );
                await dialog.ShowAsync();
            }
        }

        /// <summary>
        /// Get the activity for next day.
        /// </summary>
        /// <param name="sender">The control that the action is for.</param>
        /// <param name="e">Parameter that contains the event data.</param>
        private async void GoToNextDay( object sender, RoutedEventArgs e )
        {
            if( _iCurrentDate.Date < DateTime.Now.Date )
            {
                _iCurrentDate = _iCurrentDate.AddDays( 1 );
                await UpdateScreenAsync();
            }
            else
            {
                MessageDialog dialog = new MessageDialog( "Can't get future activties", "Activites" );
                await dialog.ShowAsync();
            }
        }

        /// <summary>
        /// Zoom level button click handler
        /// </summary>
        /// <param name="sender">Sender object</param>
        /// <param name="e">Event arguments</param>
        private async void ChangeZoom( object sender, RoutedEventArgs e )
        {
            _currentZoomLevel = ( _currentZoomLevel + 1 ) % ZOOM_LEVELS.Length;
            await UpdateScreenAsync();
        }

        /// <summary>
        /// Performs asynchronous SensorCore SDK operation and handles any exceptions
        /// </summary>
        /// <param name="action"></param>
        /// <returns><c>true</c> if call was successful, <c>false</c> otherwise</returns>
        private async Task<bool> CallSenseApiAsync( Func<Task> action )
        {
            Exception failure = null;
            try
            {
                await action();
            }
            catch( Exception e )
            {
                failure = e;
            }

            if( failure != null )
            {
                MessageDialog dialog = null;
                switch( SenseHelper.GetSenseError( failure.HResult ) )
                {
                    case SenseError.LocationDisabled:
                        dialog = new MessageDialog( "Location has been disabled. Do you want to open Location settings now?", "Information" );
                        dialog.Commands.Add( new UICommand( "Yes", new UICommandInvokedHandler( async ( cmd ) => await SenseHelper.LaunchLocationSettingsAsync() ) ) );
                        dialog.Commands.Add( new UICommand( "No" ) );
                        await dialog.ShowAsync();
                        return true;

                    case SenseError.SenseDisabled:
                        dialog = new MessageDialog( "Motion data has been disabled. Do you want to open Motion data settings now?", "Information" );
                        dialog.Commands.Add( new UICommand( "Yes", new UICommandInvokedHandler( async ( cmd ) => await SenseHelper.LaunchSenseSettingsAsync() ) ) );
                        dialog.Commands.Add( new UICommand( "No" ) );
                        await dialog.ShowAsync();
                        return true;

                    case SenseError.IncompatibleSDK:
                        dialog = new MessageDialog( "This application has become outdated. Please update to the latest version.", "Information" );
                        await dialog.ShowAsync();
                        return false;

                    default:
                        return false;
                }
            }
            else
            {
                return true;
            }
        }
    }
}
