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
using System.Windows.Shapes;
using Microsoft.Kinect;

namespace qwerty_handtracking
{
    /// <summary>
    /// MainWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class MainWindow : Window
    {
        private KinectSensor KinectSensor = null;

        private MultiSourceFrameReader MultiSourceFrameReader = null;

        private WriteableBitmap WriteableBitmap = null;        

        private FrameDescription FrameDescription = null;

        private CoordinateMapper coordinateMapper = null;        

        private Body[] bodies = null;

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

                FrameDescription = this.KinectSensor.ColorFrameSource.CreateFrameDescription(ColorImageFormat.Bgra);

                WriteableBitmap = new WriteableBitmap(FrameDescription.Width, FrameDescription.Height, 96, 96, PixelFormats.Bgr32, null);

                MultiSourceFrameReader = KinectSensor.OpenMultiSourceFrameReader(FrameSourceTypes.Color | FrameSourceTypes.Depth | FrameSourceTypes.Body);

                MultiSourceFrameReader.MultiSourceFrameArrived += Reader_MultiSourceFrameArrived;                
            }
        }

        private void MainWindow_Closed(object sender, EventArgs e)
        {
            if( MultiSourceFrameReader!= null )
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
                foreach (Body body in bodies)
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
                           // ColorSpacePoint depthSpacePoint = coordinateMapper.MapCameraPointToColorSpace(joints[jointType].Position);
                            
                            Point depthSpacePoint = Scale(joints[jointType], coordinateMapper);

                            jointPoints[jointType] = new Point(depthSpacePoint.X, depthSpacePoint.Y);
                        }
                        DrawBody(joints, jointPoints, canvas);
                        handState(body);
                    }
                }
            }
            #endregion
        }

        private void DrawBody(Dictionary<JointType,Joint> joints,Dictionary<JointType,Point> jointPoints, Canvas canvas)
        {     
            foreach(JointType jointType in joints.Keys)
            {                
                TrackingState trackingState = joints[jointType].TrackingState;

                if (trackingState == TrackingState.NotTracked)
                    return;

                Point point = jointPoints[jointType];
                Ellipse ellipse = new Ellipse
                {
                    Width = 10,
                    Height = 10,
                    Fill = new SolidColorBrush(Colors.LightBlue)
                };
                Canvas.SetLeft(ellipse, point.X - ellipse.Width / 2);
                Canvas.SetTop(ellipse, point.Y - ellipse.Height / 2);

                canvas.Children.Add(ellipse);
               
                DrawBone(joints, jointPoints, canvas);
            }
        }

        private void DrawBone(Dictionary<JointType, Joint> joints, Dictionary<JointType,Point> jointPoints, Canvas canvas)
        {
            DrawLine(joints, jointPoints, JointType.ShoulderRight, JointType.ElbowRight, canvas);
            DrawLine(joints, jointPoints, JointType.ElbowRight, JointType.WristRight, canvas);
            DrawLine(joints, jointPoints, JointType.WristRight, JointType.HandRight, canvas);
            DrawLine(joints, jointPoints, JointType.HandRight, JointType.HandTipRight, canvas);
            DrawLine(joints, jointPoints, JointType.WristRight, JointType.ThumbRight, canvas);

            DrawLine(joints, jointPoints, JointType.ShoulderLeft, JointType.ElbowLeft, canvas);
            DrawLine(joints, jointPoints, JointType.ElbowLeft, JointType.WristLeft, canvas);
            DrawLine(joints, jointPoints, JointType.WristLeft, JointType.HandLeft, canvas);
            DrawLine(joints, jointPoints, JointType.HandLeft, JointType.HandTipLeft, canvas);
            DrawLine(joints, jointPoints, JointType.WristLeft, JointType.ThumbLeft, canvas);
        }

        private void DrawLine(Dictionary<JointType, Joint> joints, Dictionary<JointType, Point> jointPoints, JointType jointType0, JointType jointType1, Canvas canvas)
        {
            Joint joint0 = joints[jointType0];
            Joint joint1 = joints[jointType1];

            if ((joint0.TrackingState == TrackingState.NotTracked) || (joint1.TrackingState == TrackingState.NotTracked))
            {
                return;
            }
            if ((joint0.TrackingState == TrackingState.Tracked) && (joint1.TrackingState == TrackingState.Tracked))
            {
                Line line = new Line
                {
                    X1 = jointPoints[jointType0].X,
                    Y1 = jointPoints[jointType0].Y,
                    X2 = jointPoints[jointType1].X,
                    Y2 = jointPoints[jointType1].Y,
                    StrokeThickness = 5,
                    Stroke = new SolidColorBrush(Colors.LightBlue)
                };

                canvas.Children.Add(line);
            }            
        }
        private Point Scale(Joint joint, CoordinateMapper mapper)
        {
            Point point = new Point();

            ColorSpacePoint colorPoint = mapper.MapCameraPointToColorSpace(joint.Position);
            point.X = float.IsInfinity(colorPoint.X) ? 0.0 : colorPoint.X;
            point.Y = float.IsInfinity(colorPoint.Y) ? 0.0 : colorPoint.Y;
                        
            return point;
        }

        private void handState(Body body)
        {
            string rightHandState = null;
            string leftHandState = null;

            switch (body.HandRightState)
            {
                case HandState.Open:
                    rightHandState = "빠";
                    break;
                case HandState.Closed:
                    rightHandState = "묵";
                    break;
                case HandState.Lasso:
                    rightHandState = "찌";
                    break;
                case HandState.Unknown:
                    rightHandState = "Unknown...";
                    break;
                case HandState.NotTracked:
                    rightHandState = "Not tracked";
                    break;
                default:
                    break;
            }

            switch (body.HandLeftState)
            {
                case HandState.Open:
                    leftHandState = "빠";
                    break;
                case HandState.Closed:
                    leftHandState = "묵";
                    break;
                case HandState.Lasso:
                    leftHandState = "찌";
                    break;
                case HandState.Unknown:
                    leftHandState = "Unknown...";
                    break;
                case HandState.NotTracked:
                    leftHandState = "Not tracked";
                    break;
                default:
                    break;
            }


            RightHandState.Text = rightHandState;
            LeftHandState.Text  = leftHandState;
        }
    }

}
