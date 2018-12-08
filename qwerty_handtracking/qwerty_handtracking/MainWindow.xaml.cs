using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
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
using System.Runtime.InteropServices;
using Excel = Microsoft.Office.Interop.Excel;
using System.Windows.Shapes;
using Microsoft.Kinect;
using LightBuzz.Vitruvius.FingerTracking;
using System.Threading;
using InTheHand.Net.Sockets;                                                                                                                                                                                                                                                
using InTheHand.Net.Bluetooth;
using System.IO;
using System.Net.Sockets;
/// <summary>
// 수화 인식용 코드
/// </summary>
namespace qwerty_handtracking
{
    /// <summary>
    /// MainWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class MainWindow : Window
    {
        private KinectSensor KinectSensor = null;

        private MultiSourceFrameReader MultiSourceFrameReader = null;

        private HandsController handsController = null;

        private WriteableBitmap WriteableBitmap = null;        

        private FrameDescription FrameDescription = null;

        private CoordinateMapper coordinateMapper = null;        

        private Body[] bodies = null;

        private Excel.Application application = null;

        private Excel.Workbook workbook = null;

        private Excel.Worksheet worksheet = null;

        private Excel.Range range = null;

        private object[,] data = null;

        private static int position_idx = 0, ExcelRow = 1, SL=0, SD_idx = 0, MatchingRate = 0;

        private double[] HandLeft_Y = new double[1000];

        private double[] HandRight_Y = new double[1000];

        private Thread AcceptAndListeningThread;

        private Boolean isConnected = false;

        private BluetoothClient BluetoothClient;

        private BluetoothListener BluetoothListener;

        private Point sholderPoint;
               
                
        public MainWindow()
        {
            InitializeComponent();
        }
        
        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            KinectSensor = KinectSensor.GetDefault();

            if ( KinectSensor != null) {

                KinectSensor = KinectSensor.GetDefault();
                
                coordinateMapper = KinectSensor.CoordinateMapper;

                KinectSensor.Open();

                application = new Excel.Application();
                /*=======================*/
                
                workbook = application.Workbooks.Open(@"C:\Temp\leftHand.xls");
                worksheet = workbook.Worksheets.get_Item(1) as Excel.Worksheet;

                Excel.Range range = worksheet.UsedRange;
                data = range.Value;
                /*=======================*/
                
                FrameDescription = this.KinectSensor.ColorFrameSource.CreateFrameDescription(ColorImageFormat.Bgra);

                WriteableBitmap = new WriteableBitmap(FrameDescription.Width, FrameDescription.Height, 96, 96, PixelFormats.Bgr32, null);

                MultiSourceFrameReader = KinectSensor.OpenMultiSourceFrameReader(FrameSourceTypes.Color | FrameSourceTypes.Depth | FrameSourceTypes.Body);

                MultiSourceFrameReader.MultiSourceFrameArrived += Reader_MultiSourceFrameArrived;

                handsController = new HandsController();

                handsController.HandsDetected += HandsController_HandsDetected;
                
            }
            /*
            if(BluetoothRadio.IsSupported)
            {
                AcceptAndListeningThread = new Thread(AcceptAndListen);

                AcceptAndListeningThread.Start();
            }
            else
            {
                Debug.WriteLine("Bluetooth not Supported!");
            } 
            */
            
    }

        private void AcceptAndListen()
        {
            while (true)
            {
                if (!isConnected)
                {
                    try
                    {
                        BluetoothListener = new BluetoothListener(BluetoothService.RFCommProtocol);

                        Debug.WriteLine("Listener created with TCP Protocol service " + BluetoothService.RFCommProtocol);
                        Debug.WriteLine("Starting Listener….");
                        BluetoothListener.Start();
                        Debug.WriteLine("Listener Started!");
                        Debug.WriteLine("Accepting incoming connection….");
                        BluetoothClient = BluetoothListener.AcceptBluetoothClient();
                        isConnected = BluetoothClient.Connected;
                        Debug.WriteLine("A Bluetooth Device Connected!");


                    }
                    catch (Exception e)
                    {
                        Debug.WriteLine("There is an error while accepting connection");
                        Debug.WriteLine(e.Message);
                        Debug.WriteLine("Retrying….");
                    }
                }
                else
                {
                    try
                    {
                       Debug.WriteLine("Listening….");
                        NetworkStream stream = BluetoothClient.GetStream();

                        Byte[] bytes = new Byte[512];

                        String retrievedMsg = "";

                        stream.Read(bytes, 0, 512);

                        stream.Flush();

                        for (int i = 0; i < bytes.Length; i++)
                        {
                            retrievedMsg += Convert.ToChar(bytes[i]);

                        }
                        Debug.WriteLine(retrievedMsg);

                        if (retrievedMsg.Contains("finish"))
                        {
                            Debug.WriteLine(BluetoothClient.Connected);
                            BluetoothClient.GetStream().Close();
                            BluetoothClient.Dispose();
                            BluetoothListener.Stop();
                            isConnected = false;

                            continue;
                        }


                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("There is an error while listening connection");
                        Debug.WriteLine(ex.Message);
                        isConnected = BluetoothClient.Connected;
                    }
                }
            }             


        }

        private void MainWindow_Closed(object sender, EventArgs e)
        {
           /*             
            AcceptAndListeningThread.Abort();
            BluetoothClient.GetStream().Close();
            BluetoothClient.Dispose();
            BluetoothListener.Stop();
            */
            workbook.Close();
            application.Quit();
            worksheet = null;
            workbook = null;
            application = null;
         

            if ( MultiSourceFrameReader!= null )
            {
                MultiSourceFrameReader.Dispose();
            }

            if( KinectSensor != null)
            {
                KinectSensor.Close();
            }
           
            
        }   
     
        private void Reader_MultiSourceFrameArrived(object sender, MultiSourceFrameArrivedEventArgs e)
        {
            var frame = e.FrameReference.AcquireFrame();
            
            #region colorFrame
            using ( ColorFrame colorFrame = frame.ColorFrameReference.AcquireFrame() )
            {
                if( colorFrame != null )
                {
                    FrameDescription frameDescription = colorFrame.FrameDescription;

                    using ( KinectBuffer colorBuffer = colorFrame.LockRawImageBuffer() )
                    {
                        WriteableBitmap.Lock();

                        if( (frameDescription.Width == WriteableBitmap.PixelWidth) && (frameDescription.Height == WriteableBitmap.PixelHeight))
                        {
                            colorFrame.CopyConvertedFrameDataToIntPtr(WriteableBitmap.BackBuffer, (uint)(frameDescription.Width * frameDescription.Height * 4), ColorImageFormat.Bgra);

                            WriteableBitmap.AddDirtyRect(new Int32Rect(0, 0, WriteableBitmap.PixelWidth, WriteableBitmap.PixelHeight));
                        }

                        WriteableBitmap.Unlock();
                    }
                }
                camera.Source = WriteableBitmap;
            }
            #endregion

            #region handTracking
            bool dataReceived = false;
            canvas.Children.Clear();

            using (BodyFrame bodyFrame = frame.BodyFrameReference.AcquireFrame())
            {
                if (bodyFrame != null)
                {
                    if (bodies == null)
                    {
                        bodies = new Body[bodyFrame.BodyCount];
                    }

                    bodyFrame.GetAndRefreshBodyData(bodies);
                    dataReceived = true;
                }
            }
            if (dataReceived)
            {
                //body = bodies.Where(b => b.IsTracked).FirstOrDefault();
                
                foreach(Body body in bodies)
                {
                    if (body.IsTracked)
                    {
                        Dictionary<JointType, Joint> joints = new Dictionary<JointType, Joint>();

                        joints[JointType.ShoulderRight] = body.Joints[JointType.ShoulderRight];
                        joints[JointType.ElbowRight] = body.Joints[JointType.ElbowRight];
                        joints[JointType.WristRight] = body.Joints[JointType.WristRight];
                        joints[JointType.HandRight] = body.Joints[JointType.HandRight];
                        joints[JointType.ThumbRight] = body.Joints[JointType.ThumbRight];
                        joints[JointType.HandTipRight] = body.Joints[JointType.HandTipRight];
                        joints[JointType.ShoulderLeft] = body.Joints[JointType.ShoulderLeft];
                        joints[JointType.ElbowLeft] = body.Joints[JointType.ElbowLeft];
                        joints[JointType.WristLeft] = body.Joints[JointType.WristLeft];
                        joints[JointType.HandLeft] = body.Joints[JointType.HandLeft];
                        joints[JointType.ThumbLeft] = body.Joints[JointType.ThumbLeft];
                        joints[JointType.HandTipLeft] = body.Joints[JointType.HandTipLeft];

                        Dictionary<JointType, Point> jointPoints = new Dictionary<JointType, Point>();
                                                
                        foreach (JointType jointType in joints.Keys)
                        {
                            
                            Point colorSpacePoint = Scale(joints[jointType], coordinateMapper);

                            jointPoints[jointType] = new Point(colorSpacePoint.X, colorSpacePoint.Y);                                                   

                        }

                        if (position_idx == 1)
                            RecognizeStart(jointPoints);

                        DrawRec(canvas, jointPoints[JointType.HandLeft], jointPoints[JointType.ShoulderLeft]);

                        DrawBody(joints, jointPoints, canvas);
                                                
                        
                    }                    
                    
                }
                
            }
            #endregion

            #region fingerTracking
            using (DepthFrame depthFrame = frame.DepthFrameReference.AcquireFrame())
            {
                if(depthFrame != null)
                {
                    using(KinectBuffer kinectBuffer = depthFrame.LockImageBuffer())
                    {
                        foreach(Body body in bodies)
                        {
                            handsController.Update(kinectBuffer.UnderlyingBuffer, body);
                        }                        
                    }
                }
            }
            #endregion
        }

        public Boolean sendMessage(String msg)
        {            
            try
            {                
                if (!msg.Equals(""))
                {                    
                    UTF8Encoding encoder = new UTF8Encoding();
                    NetworkStream ns = BluetoothClient.GetStream();
                    StreamWriter sw = new StreamWriter(ns);
                    sw.WriteLine(msg, System.Text.Encoding.Default);
                    sw.Flush();                                       
                }
            }
            catch (Exception)
            {
                Debug.WriteLine("There is an error while sending message");                
                try
                {
                    isConnected = BluetoothClient.Connected;
                    BluetoothClient.GetStream().Close();
                    BluetoothClient.Dispose();
                    BluetoothListener.Server.Dispose();
                    BluetoothListener.Stop();
                }
                catch (Exception)
                {
                }

                return false;
            }

            return true;
        }        

        private void DrawRec(Canvas canvas, Point point, Point sPoint)
        {
            int prePosition_x = 700;
            int prePosition_y = 600;
            int postPosition_x = 300;
            int postPosition_y = 600;
            

            Rectangle rec1 = new Rectangle
            {
                Width = 200,
                Height = 200,
                Stroke = Brushes.Red               
            };

            Rectangle rec2 = new Rectangle
            {
                Width = 200,
                Height = 200,
                Stroke = Brushes.Red
            };
            

            if (prePosition_x + 50 < point.X && point.X < prePosition_x + 150 && 
                prePosition_y + 50 < point.Y && point.Y < prePosition_y + 150 && position_idx == 0)
            {
                position_idx = 1;
                
                sholderPoint = sPoint;
            }
                

            else if (postPosition_x + 50 < point.X && point.X < postPosition_x + 150 &&
                     postPosition_y + 50 < point.Y && point.Y < postPosition_y + 150 && position_idx == 1)
            {
                position_idx = 0;
                CalculateResult();
            }                


            if (position_idx == 0)
            {
                rec1.Margin = new Thickness(prePosition_x,       prePosition_y, 0, 0);
                rec2.Margin = new Thickness(prePosition_x + 300, prePosition_y, 0, 0);
            }
                
            else if (position_idx == 1)
            {
                rec1.Margin = new Thickness(postPosition_x,       postPosition_y, 0, 0);
                rec2.Margin = new Thickness(postPosition_x + 900, postPosition_y, 0, 0);
            }
                
            else
            {
                canvas.Children.Remove(rec1);
                canvas.Children.Remove(rec2);
            }

            canvas.Children.Add(rec1);
            canvas.Children.Add(rec2);


        }

        private void DrawBody(Dictionary<JointType,Joint> joints,Dictionary<JointType,Point> jointPoints, Canvas canvas)
        {     
            foreach(JointType jointType in joints.Keys)
            {                
                TrackingState trackingState = joints[jointType].TrackingState;

                if (trackingState == TrackingState.NotTracked)
                    return;

                Point point = jointPoints[jointType];

                DrawEllipse(point, Brushes.LightBlue, 4);
                              
                DrawBone(joints, jointPoints, canvas);
            }
        }

        private void DrawBone(Dictionary<JointType, Joint> joints, Dictionary<JointType,Point> jointPoints, Canvas canvas)
        {
            DrawLine(jointPoints[JointType.ShoulderRight], jointPoints[JointType.ElbowRight], canvas);
            DrawLine(jointPoints[JointType.ElbowRight], jointPoints[JointType.WristRight], canvas);
            DrawLine(jointPoints[JointType.WristRight], jointPoints[JointType.HandRight], canvas);
            DrawLine(jointPoints[JointType.HandRight], jointPoints[JointType.HandTipRight], canvas);
            DrawLine(jointPoints[JointType.WristRight], jointPoints[JointType.ThumbRight], canvas);
            
            DrawLine(jointPoints[JointType.ShoulderLeft], jointPoints[JointType.ElbowLeft], canvas);
            DrawLine(jointPoints[JointType.ElbowLeft], jointPoints[JointType.WristLeft], canvas);
            DrawLine(jointPoints[JointType.WristLeft], jointPoints[JointType.HandLeft], canvas);
            DrawLine(jointPoints[JointType.HandLeft], jointPoints[JointType.HandTipLeft], canvas);
            DrawLine(jointPoints[JointType.WristLeft], jointPoints[JointType.ThumbLeft], canvas);
        }

        private void DrawLine(Point point1, Point point2, Canvas canvas)
        {            

            if (double.IsInfinity(point1.X) || double.IsInfinity(point1.Y) || double.IsInfinity(point2.X) || double.IsInfinity(point2.Y))
            {
                return;
            }
            if (!double.IsInfinity(point1.X) && !double.IsInfinity(point1.Y) && !double.IsInfinity(point2.X) && !double.IsInfinity(point2.Y))
            {
                Line line = new Line
                {
                    X1 = point1.X,
                    Y1 = point1.Y,
                    X2 = point2.X,
                    Y2 = point2.Y,
                    StrokeThickness = 5,
                    Stroke = new SolidColorBrush(Colors.LightBlue)
                };

                canvas.Children.Add(line);
            }            
        }

        private void DrawEllipse(Point point, Brush brush, double radius)
        {
            Ellipse ellipse = new Ellipse
            {
                Width = radius,
                Height = radius,
                Fill = brush
            };

            canvas.Children.Add(ellipse);

            Canvas.SetLeft(ellipse, point.X - radius / 2.0);
            Canvas.SetTop(ellipse, point.Y - radius / 2.0);
        }

        private Point Scale(Joint joint, CoordinateMapper mapper)
        {
            Point point = new Point();

            ColorSpacePoint colorPoint = mapper.MapCameraPointToColorSpace(joint.Position);
            DepthSpacePoint depthSpacePoint = mapper.MapCameraPointToDepthSpace(joint.Position);
            
            point.X = float.IsInfinity(colorPoint.X) ? 0.0 : colorPoint.X;
            point.Y = float.IsInfinity(colorPoint.Y) ? 0.0 : colorPoint.Y;
                        
            return point;
        }

        private void RecognizeStart(Dictionary<JointType, Point> jointPoints)
        {            
            HandLeft_Y[SD_idx] = jointPoints[JointType.HandLeft].Y;
            HandRight_Y[SD_idx] = jointPoints[JointType.HandRight].Y;
            SD_idx++;
            SignLanguage.Text = "- ";
            CorrectRate.Text = "- ";
        }

        private async void CalculateResult()
        {
            var task = Task.Run(() => Regularization());
            await task;

            SignLanguage.Text = "- " + data[SL, 162].ToString();
            sendMessage(data[SL, 162].ToString());
            CorrectRate.Text = "- " + (MatchingRate * 100 / 160).ToString() + "%";
            MatchingRate = 0;
            ExcelRow++;
            SD_idx = 0;
        }

        private void Regularization()
        {
            double SN;
            double rHL;
            double rHR;
            double HeightRegularize;
            var ExcelSize = data.GetLength(0);

            
            int[] sum = new int[ExcelSize];
            

            for (int i = 2; i <= 81; i++)
            {
                SN = SD_idx * i / 80;
                int dec = (int)SN;

                if (i == 80)
                {
                    rHL = HandLeft_Y[dec];
                    rHR = HandRight_Y[dec];
                }


                else
                {
                    rHL = HandLeft_Y[dec] * (1 - (SN - dec)) + HandLeft_Y[dec + 1] * (SN - dec);
                    rHR = HandRight_Y[dec] * (1 - (SN - dec)) + HandRight_Y[dec + 1] * (SN - dec); 
                }
                    

                for (int j = 1; j <= ExcelSize; j++)
                {
                    HeightRegularize = double.Parse(data[j, 1].ToString()) - sholderPoint.Y;
                    
                    double leftGap  = double.Parse(data[j, i].ToString()) - HeightRegularize - rHL;
                    double rightGap = double.Parse(data[j, i+80].ToString()) - HeightRegularize - rHR;

                    if (-50 < leftGap && leftGap < 50)
                        sum[j - 1]++;

                    if(-50 < rightGap && rightGap < 50)
                        sum[j - 1]++;
                }
                

            }
            for (int i = 0; i < ExcelSize; i++)
            {
                if (MatchingRate < sum[i])
                {
                    MatchingRate = sum[i];                    
                    
                    SL = i + 1;
                }
                
            }            
      
        }

        private void HandsController_HandsDetected(object sender, HandCollection e)
        {
            // Display the results!
            foreach(Body body in bodies)
            {
                if(body.TrackingId == e.TrackingId)
                {
                    if (e.HandLeft != null)
                    {
                        // Draw fingers.
                        foreach (var finger in e.HandLeft.Fingers)
                        {
                            ColorSpacePoint center = coordinateMapper.MapCameraPointToColorSpace(body.Joints[JointType.WristLeft].Position);
                            Point CenterPosition = new Point(center.X, center.Y);
                            Point point = new Point(finger.ColorPoint.X, finger.ColorPoint.Y);
                            DrawEllipse(point, Brushes.Yellow, 20.0);
                            
                        }
                    }

                    if (e.HandRight != null)
                    {
                        // Draw fingers.
                        foreach (var finger in e.HandRight.Fingers)
                        {
                            ColorSpacePoint center = coordinateMapper.MapCameraPointToColorSpace(body.Joints[JointType.WristRight].Position);
                            Point CenterPosition = new Point(center.X, center.Y);
                            Point point = new Point(finger.ColorPoint.X, finger.ColorPoint.Y);
                            DrawEllipse(point, Brushes.Yellow, 20.0);
                            
                        }
                    }
                }                
            }            
        }

    }
}
