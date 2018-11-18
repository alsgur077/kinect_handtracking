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

        private WriteableBitmap WriteableBitmap = null;

        private MultiSourceFrameReader MultiSourceFrameReader = null;

        private FrameDescription FrameDescription = null;

        private Body[] bodies = null;

        private CoordinateMapper coordinateMapper = null;

        private const double JointThickness = 3;

        private readonly Brush trackedJointBrush = new SolidColorBrush(Color.FromArgb(255, 68, 192, 68));

        private readonly Brush inferredJointBrush = Brushes.Yellow;

        private readonly Pen inferredBonePen = new Pen(Brushes.Gray, 1);

        private List<Tuple<JointType, JointType>> bones;

        private DrawingGroup drawingGroup;

        private DrawingImage imageSource;

        private int displayWidth;

        private int displayHeight;

        private List<Pen> bodyColors = null;

        public MainWindow()
        {
            KinectSensor = KinectSensor.GetDefault();
            this.coordinateMapper = this.KinectSensor.CoordinateMapper;

            KinectSensor.Open();

            FrameDescription = this.KinectSensor.ColorFrameSource.CreateFrameDescription(ColorImageFormat.Bgra);

            WriteableBitmap = new WriteableBitmap(FrameDescription.Width, FrameDescription.Height, 96, 96, PixelFormats.Bgr32, null);

            MultiSourceFrameReader = KinectSensor.OpenMultiSourceFrameReader(FrameSourceTypes.Color | FrameSourceTypes.Depth | FrameSourceTypes.Body);


            this.bones = new List<Tuple<JointType, JointType>>();

            this.bones.Add(new Tuple<JointType, JointType>(JointType.ShoulderRight, JointType.ElbowRight));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.ElbowRight, JointType.WristRight));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.WristRight, JointType.HandRight));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.HandRight, JointType.HandTipRight));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.WristRight, JointType.ThumbRight));

            this.bodyColors = new List<Pen>();

            this.bodyColors.Add(new Pen(Brushes.Red, 6));
            this.bodyColors.Add(new Pen(Brushes.Orange, 6));
            this.bodyColors.Add(new Pen(Brushes.Green, 6));
            this.bodyColors.Add(new Pen(Brushes.Blue, 6));
            this.bodyColors.Add(new Pen(Brushes.Indigo, 6));
            this.bodyColors.Add(new Pen(Brushes.Violet, 6));

            this.drawingGroup = new DrawingGroup();

            this.imageSource = new DrawingImage(this.drawingGroup);

            this.DataContext = this;

            InitializeComponent();
        }
        
        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            
            if ( KinectSensor != null) {
                                
                

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
     
        private ImageSource ImageSource
        {
            get
            {
                return imageSource;
            }
        }
       
        private void Reader_MultiSourceFrameArrived(object sender, MultiSourceFrameArrivedEventArgs e)
        {
            var frame = e.FrameReference.AcquireFrame();

            // color 영상 출력 부분
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

            // handtracking
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
                using (DrawingContext dc = drawingGroup.Open())
                {
                    
                    int penIndex = 0;

                    foreach (Body body in bodies)
                    {
                        Pen drawPen = bodyColors[penIndex++];
                        if (body.IsTracked)
                        {
                            Joint ShoulderRight = body.Joints[JointType.ShoulderRight];
                            Joint ElbowRight = body.Joints[JointType.ElbowRight];
                            Joint WristRight = body.Joints[JointType.WristRight];
                            Joint HandRight = body.Joints[JointType.HandRight];
                            Joint ThumbRight = body.Joints[JointType.ThumbRight];
                            Joint HandTipRight = body.Joints[JointType.HandTipRight];

                            Dictionary<JointType, Joint> joints = new Dictionary<JointType, Joint>();
                            joints[JointType.ShoulderRight] = ShoulderRight;
                            joints[JointType.ElbowRight] = ElbowRight;
                            joints[JointType.WristRight] = WristRight;
                            joints[JointType.HandRight] = HandRight;
                            joints[JointType.ThumbRight] = ThumbRight;
                            joints[JointType.HandTipRight] = HandTipRight;

                            Dictionary<JointType, Point> jointPoints = new Dictionary<JointType, Point>();

                            foreach (JointType jointType in joints.Keys)
                            {                                
                                CameraSpacePoint position = joints[jointType].Position;
                                
                                DepthSpacePoint depthSpacePoint = coordinateMapper.MapCameraPointToDepthSpace(position);
                                
                                jointPoints[jointType] = new Point(depthSpacePoint.X, depthSpacePoint.Y);
                            }
                            DrawBody(joints, jointPoints, dc, drawPen);
                        }
                    }
                }
            }           
        }

        private void DrawBody(Dictionary<JointType,Joint> joints,Dictionary<JointType,Point> jointPoints, DrawingContext drawingContext, Pen drawingPen)
        {
            foreach(var bone in bones)
            {
             
                DrawBone(joints, jointPoints, bone.Item1, bone.Item2, drawingContext, drawingPen);
            }

            foreach(JointType jointType in joints.Keys)
            {
                Brush drawBrush = null;

                TrackingState trackingState = joints[jointType].TrackingState;

                if(trackingState == TrackingState.Tracked)
                {
                    drawBrush = trackedJointBrush;
                }
                else if(trackingState == TrackingState.Inferred)
                {
                    drawBrush = inferredJointBrush;
                }

                if(drawBrush != null)
                {
                    Debug.WriteLine("draw");
                    drawingContext.DrawEllipse(drawBrush, null, jointPoints[jointType], JointThickness, JointThickness);
                }
            }
        }

        private void DrawBone(Dictionary<JointType, Joint> joints, Dictionary<JointType, Point> jointPoints, JointType jointType0, JointType jointType1, DrawingContext drawingContext, Pen drawingPen)
        {
            Joint joint0 = joints[jointType0];
            Joint joint1 = joints[jointType1];

            if((joint0.TrackingState == TrackingState.NotTracked) || (joint1.TrackingState == TrackingState.NotTracked))
            {
                return;
            }

            Pen drawPen = inferredBonePen;

            if ((joint0.TrackingState == TrackingState.Tracked) && (joint1.TrackingState == TrackingState.Tracked))
            {
                drawPen = drawingPen;
            }

            drawingContext.DrawLine(drawPen, jointPoints[jointType0], jointPoints[jointType1]);
        }        
    }

}
