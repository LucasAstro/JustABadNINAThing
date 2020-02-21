using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using NINA.Utility.Mediator; // A lot of things now where do we call this No the class we just made you dumbo
using System.Threading.Tasks;
using System.Net;
using NINA.EquipmentChooser;
using System.Threading;
using System.IO;
using NINA.Model.MyTelescope; // Now we find out where the heck to call this abomination
using NINA.ViewModel;
using NINA.ViewModel.Equipment.Telescope;
using System.Windows;
using System.ComponentModel;
using NINA.Model;
using NINA.Utility.Astrometry;
using NINA.Model.MyCamera;
using NINA.Utility.Notification;
using NINA.Utility;
using NINA.Model.MyFilterWheel;

namespace Projector
{
    class Projectify : BaseINPC
    {


        public System.ComponentModel.BackgroundWorker httpServer;
        public Projectify()
        {
            this.httpServer = new System.ComponentModel.BackgroundWorker();
            this.httpServer.DoWork += new System.ComponentModel.DoWorkEventHandler(this.Projectionify);
        }
        public static AltAz Calculate(double RA, double Dec, double Lat, double Long)
        {
            return Calculate(RA, Dec, Lat, Long, DateTime.UtcNow);
        }
        public static AltAz Calculate(double RA, double Dec, double Lat, double Long, DateTime Date)
        {
            // Day offset and Local Siderial Time
            double dayOffset = (Date - new DateTime(2000, 1, 1, 12, 0, 0, DateTimeKind.Utc)).TotalDays;
            double LST = (100.46 + 0.985647 * dayOffset + Long + 15 * (Date.Hour + Date.Minute / 60d) + 360) % 360;

            // Hour Angle
            double HA = (LST - RA + 360) % 360;

            // HA, DEC, Lat to Alt, AZ
            double x = Math.Cos(HA * (Math.PI / 180)) * Math.Cos(Dec * (Math.PI / 180));
            double y = Math.Sin(HA * (Math.PI / 180)) * Math.Cos(Dec * (Math.PI / 180));
            double z = Math.Sin(Dec * (Math.PI / 180));

            double xhor = x * Math.Cos((90 - Lat) * (Math.PI / 180)) - z * Math.Sin((90 - Lat) * (Math.PI / 180));
            double yhor = y;
            double zhor = x * Math.Sin((90 - Lat) * (Math.PI / 180)) + z * Math.Cos((90 - Lat) * (Math.PI / 180));

            double az = Math.Atan2(yhor, xhor) * (180 / Math.PI) + 180;
            double alt = Math.Asin(zhor) * (180 / Math.PI);

            return new AltAz() { Alt = alt, Az = az };
        } // We are going to do something really dirty that might not work

        async private void startLiveView()
        {
            await AppVM.ImagingVM.StartLiveView();
        }

        public class AltAz
        {
            public double Alt { get; set; }
            public double Az { get; set; }
        }

        public static class ImageTypes
        {
            public const string LIGHT = "LIGHT";
            public const string FLAT = "FLAT";
            public const string DARK = "DARK";
            public const string BIAS = "BIAS";
            public const string DARKFLAT = "DARKFLAT";
            public const string SNAPSHOT = "SNAPSHOT";
        }

        public ApplicationVM AppVM = (ApplicationVM)Application.Current.Resources["AppVM"];
        public TelescopeInfo TelescopeInfo;

        async public void CallToChildThread()
        {
            Progress<ApplicationStatus> appStatus = new Progress<ApplicationStatus>(p => Status = p);
            AppVM.ImagingVM.CancelSnapCommand.Execute(appStatus);
        }

        private ApplicationStatus _status;

        public ApplicationStatus Status
        {
            get
            {
                return _status;
            }
            set
            {
                _status = value;
                _status.Source = AppVM.Title;

                RaisePropertyChanged();

                AppVM.ApplicationStatusVM.applicationStatusMediator.StatusUpdate(_status);
            }
        }

        async public void Projectionify(object sender, DoWorkEventArgs e)
        {
            ThreadStart childRef = new ThreadStart(CallToChildThread);
            BackgroundWorker worker = sender as BackgroundWorker;
            TelescopeInfo = AppVM.TelescopeVM.TelescopeInfo;

            Console.WriteLine(TelescopeInfo);


            HttpListener listener = new HttpListener();

            listener.Prefixes.Add("http://localhost:7654/");
            listener.Start();


            while (listener.IsListening)
            {
                HttpListenerContext context = listener.GetContext();
                HttpListenerRequest request = context.Request;
                HttpListenerResponse response = context.Response;

                response.Headers.Add("Access-Control-Allow-Origin", "*");
                response.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Accept, X-Requested-With");
                response.Headers.Add("Access-Control-Allow-Methods", "GET, POST");
                response.Headers.Add("Access-Control-Max-Age", "1728000");

                var body = new StreamReader(context.Request.InputStream, request.ContentEncoding).ReadToEnd();
                var param = body.Split(',');


                Thread childThread = new Thread(childRef);

                //try
                {

                    if (param[0] == "0")
                    {

                        var rightAscension = Convert.ToDouble(param[1]);
                        var declination = Convert.ToDouble(param[2]);

                        Notification.ShowSuccess(rightAscension + "\n" + declination);

                        if (rightAscension < 0)
                        {
                            if (Calculate(rightAscension, declination, TelescopeInfo.SiteLatitude, TelescopeInfo.SiteLongitude).Alt < 30)
                            {

                                response.StatusDescription = "ERR";
                                response.StatusCode = (int)HttpStatusCode.OK;
                                byte[] below30_output = Encoding.UTF8.GetBytes("OBJECT_BELOW_30");
                                Stream stream = response.OutputStream;
                                stream.Write(below30_output, 0, below30_output.Length);
                                response.Close();
                                Notification.ShowError("Object is below 30 degrees");
                                continue;
                            }
                     
                            await AppVM.TelescopeVM.SlewToCoordinatesAsync(new Coordinates(rightAscension, declination, Epoch.J2000, Coordinates.RAType.Degrees));

                            try { AppVM.ImagingVM.CancelSnapCommand.Execute(new Object()); }
                            catch { }

                            CaptureSequenceList csl = new CaptureSequenceList { Coordinates = new Coordinates(rightAscension, declination, Epoch.J2000, Coordinates.RAType.Degrees), CenterTarget = true };

                            Progress<ApplicationStatus> appStatus = new Progress<ApplicationStatus>(p => Status = p);

                            var Caltok = new CancellationTokenSource();
                            var centerTarget = await AppVM.SeqVM.CenterTarget(csl, appStatus, Caltok);

                            if (!centerTarget.Success) { Console.WriteLine("Center target is FIRED!"); }
                            childThread.Start();
                        }
                        else
                        {
                            if (Calculate(rightAscension, declination, TelescopeInfo.SiteLatitude, TelescopeInfo.SiteLongitude).Alt < 30)
                            {
                                response.StatusDescription = "ERR";
                                response.StatusCode = (int)HttpStatusCode.OK;
                                byte[] below30_output = Encoding.UTF8.GetBytes("OBJECT_BELOW_30");
                                Stream stream = response.OutputStream;
                                stream.Write(below30_output, 0, below30_output.Length);
                                response.Close();
                                Notification.ShowError("Object is below 30 degrees");
                                continue;
                            }

                            await AppVM.TelescopeVM.SlewToCoordinatesAsync(new Coordinates(rightAscension, declination, Epoch.J2000, Coordinates.RAType.Degrees));

                            try { AppVM.ImagingVM.CancelSnapCommand.Execute(new Object()); }
                            catch { }

                            CaptureSequenceList csl = new CaptureSequenceList { Coordinates = new Coordinates(rightAscension, declination, Epoch.J2000, Coordinates.RAType.Degrees), CenterTarget = true };

                            Progress<ApplicationStatus> appStatus = new Progress<ApplicationStatus>(p => Status = p);

                            var Caltok = new CancellationTokenSource();

                            var CancelledToken = new CancellationTokenSource();

                            var centerTarget = await AppVM.SeqVM.CenterTarget(csl, appStatus, Caltok);

                            if (!centerTarget.Success) { Console.WriteLine("Center target is FIRED!"); }

                            childThread.Start();
                        }

                        response.Headers.Add("Access-Control-Allow-Origin", "*");
                        response.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Accept, X-Requested-With");
                        response.Headers.Add("Access-Control-Allow-Methods", "GET, POST");
                        response.Headers.Add("Access-Control-Max-Age", "1728000");

                        response.StatusDescription = "OK";
                        response.StatusCode = (int)HttpStatusCode.OK;

                        byte[] output = Encoding.UTF8.GetBytes("OK");
                        Stream output_stream = response.OutputStream;
                        output_stream.Write(output, 0, output.Length);

                        response.Close();
                    }

                    if (param[0] == "1")
                    {
                        byte[] output = Encoding.UTF8.GetBytes("OK");

                        response.Headers.Add("Access-Control-Allow-Origin", "*");
                        response.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Accept, X-Requested-With");
                        response.Headers.Add("Access-Control-Allow-Methods", "GET, POST");
                        response.Headers.Add("Access-Control-Max-Age", "1728000");

                        //telescope.SyncToTarget();
                        //telescope.Tracking = true;

                        Stream output_stream = response.OutputStream;
                        output_stream.Write(output, 0, output.Length);


                        response.Close();
                    }
                }
                /*catch
                {
                    byte[] output = Encoding.UTF8.GetBytes("ERR");
                    response.StatusDescription = "ERR";
                    response.StatusCode = (int)HttpStatusCode.BadRequest;

                    Stream output_stream = response.OutputStream;
                    output_stream.Write(output, 0, output.Length);

                }*/
                        }

            AppVM.TelescopeVM.Telescope.Park();
            AppVM.TelescopeVM.Telescope.Disconnect();

            listener.Stop();
            httpServer.CancelAsync();
        }

    }
}
