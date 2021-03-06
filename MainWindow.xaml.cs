﻿//------------------------------------------------------------------------------
// <copyright file="MainWindow.xaml.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

namespace Microsoft.Samples.Kinect.BodyBasics
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Windows;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;
    using Microsoft.Kinect;
    using System.Text;

    /// <summary>
    /// Interaction logic for MainWindow
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        
        /// <summary>
        /// Floor center: coordinate X
        /// </summary>
        private const double FloorCenterX = 0.0;

        /// <summary>
        /// Floor center: coordinate Y
        /// </summary>
        private const double FloorCenterY = -1.0;

        /// <summary>
        /// Floor center: coordinate Z
        /// </summary>
        private const double FloorCenterZ = 2.5;

        /// <summary>
        /// User head height
        /// </summary>
        private double HeadHeight = -10;

        /// <summary>
        /// True if position has been recognized, false otherwise
        /// </summary>
        private bool PositionDone = false;

        /// <summary>
        /// True if user has the arrow to shot it, false otherwise
        /// </summary>
        private bool arrowInHand = false;

        /// <summary>
        /// Radius of concentric circles
        /// </summary>
        private double[] DartBoardRadius = new double[5];

        /// <summary>
        /// Center of dartboard
        /// </summary>
        private Joint DartBoardCenter = new Joint();

        /// <summary>
        /// True if hand is closed, false otherwise
        /// </summary>
        private bool HandClosed = false;

        /// <summary>
        /// True if user selected if he is left or right handed, false otherwise
        /// </summary>
        private bool userSelectedHand = false;

        /// <summary>
        /// True if user is right handed, false otherwise
        /// </summary>
        private bool rightHanded = false;

        /// <summary>
        /// User puntuation
        /// </summary>
        private int userTotalPuntuation = 0;

        /// <summary>
        /// User puntuation if he shots in that moment
        /// </summary>
        private int userPointsIfShots = 0;

        /// <summary>
        /// Initial and final point to shot
        /// </summary>
        Joint initShot = new Joint();
        Joint finalShot = new Joint();

        /// <summary>
        /// Radius of drawn hand circles
        /// </summary>
        private const double HandSize = 30;

        /// <summary>
        /// Thickness of drawn joint lines
        /// </summary>
        private const double JointThickness = 3;

        /// <summary>
        /// Thickness of clip edge rectangles
        /// </summary>
        private const double ClipBoundsThickness = 10;

        /// <summary>
        /// Constant for clamping Z values of camera space points from being negative
        /// </summary>
        private const float InferredZPositionClamp = 0.1f;

        /// <summary>
        /// Brush used for drawing hands that are currently tracked as closed
        /// </summary>
        private readonly Brush handClosedBrush = new SolidColorBrush(Color.FromArgb(128, 255, 0, 0));

        /// <summary>
        /// Brush used for drawing hands that are currently tracked as opened
        /// </summary>
        private readonly Brush handOpenBrush = new SolidColorBrush(Color.FromArgb(128, 0, 255, 0));

        /// <summary>
        /// Brush used for drawing hands that are currently tracked as in lasso (pointer) position
        /// </summary>
        private readonly Brush handLassoBrush = new SolidColorBrush(Color.FromArgb(128, 0, 0, 255));

        /// <summary>
        /// Brush used for drawing joints that are currently tracked
        /// </summary>
        private readonly Brush trackedJointBrush = new SolidColorBrush(Color.FromArgb(255, 68, 192, 68));

        /// <summary>
        /// Brush used for drawing joints that are currently inferred
        /// </summary>        
        private readonly Brush inferredJointBrush = Brushes.Yellow;

        /// <summary>
        /// Pen used for drawing bones that are currently inferred
        /// </summary>        
        private readonly Pen inferredBonePen = new Pen(Brushes.Gray, 1);

        /// <summary>
        /// Drawing group for body rendering output
        /// </summary>
        private DrawingGroup drawingGroup;

        /// <summary>
        /// Drawing image that we will display
        /// </summary>
        private DrawingImage imageSource;

        /// <summary>
        /// Active Kinect sensor
        /// </summary>
        private KinectSensor kinectSensor = null;

        /// <summary>
        /// Coordinate mapper to map one type of point to another
        /// </summary>
        private CoordinateMapper coordinateMapper = null;

        /// <summary>
        /// Reader for body frames
        /// </summary>
        private BodyFrameReader bodyFrameReader = null;

        /// <summary>
        /// Array for the bodies
        /// </summary>
        private Body[] bodies = null;

        /// <summary>
        /// definition of bones
        /// </summary>
        private List<Tuple<JointType, JointType>> bones;

        /// <summary>
        /// Width of display (depth space)
        /// </summary>
        private int displayWidth;

        /// <summary>
        /// Height of display (depth space)
        /// </summary>
        private int displayHeight;

        /// <summary>
        /// List of colors for each body tracked
        /// </summary>
        private List<Pen> bodyColors;

        /// <summary>
        /// Current status text to display
        /// </summary>
        private string statusText = null;

        /// <summary>
        /// Initial milliseconds
        /// </summary>
        long currentMilliseconds;

        /// <summary>
        /// Real gameplay time
        /// </summary>
        float time;

        /// <summary>
        /// True if we have started to play on the dartboard, false otherwise
        /// </summary>
        bool activateTime = true;


        /// <summary>
        /// Initializes a new instance of the MainWindow class.
        /// </summary>
        public MainWindow()
        {
            // one sensor is currently supported
            this.kinectSensor = KinectSensor.GetDefault();

            // get the coordinate mapper
            this.coordinateMapper = this.kinectSensor.CoordinateMapper;

            // get the depth (display) extents
            FrameDescription frameDescription = this.kinectSensor.DepthFrameSource.FrameDescription;

            // get size of joint space
            this.displayWidth = frameDescription.Width;
            this.displayHeight = frameDescription.Height;

            // open the reader for the body frames
            this.bodyFrameReader = this.kinectSensor.BodyFrameSource.OpenReader();

            // a bone defined as a line between two joints
            this.bones = new List<Tuple<JointType, JointType>>();

            // Torso
            this.bones.Add(new Tuple<JointType, JointType>(JointType.Head, JointType.Neck));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.Neck, JointType.SpineShoulder));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.SpineShoulder, JointType.SpineMid));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.SpineMid, JointType.SpineBase));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.SpineShoulder, JointType.ShoulderRight));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.SpineShoulder, JointType.ShoulderLeft));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.SpineBase, JointType.HipRight));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.SpineBase, JointType.HipLeft));

            // Right Arm
            this.bones.Add(new Tuple<JointType, JointType>(JointType.ShoulderRight, JointType.ElbowRight));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.ElbowRight, JointType.WristRight));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.WristRight, JointType.HandRight));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.HandRight, JointType.HandTipRight));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.WristRight, JointType.ThumbRight));

            // Left Arm
            this.bones.Add(new Tuple<JointType, JointType>(JointType.ShoulderLeft, JointType.ElbowLeft));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.ElbowLeft, JointType.WristLeft));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.WristLeft, JointType.HandLeft));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.HandLeft, JointType.HandTipLeft));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.WristLeft, JointType.ThumbLeft));

            // Right Leg
            this.bones.Add(new Tuple<JointType, JointType>(JointType.HipRight, JointType.KneeRight));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.KneeRight, JointType.AnkleRight));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.AnkleRight, JointType.FootRight));

            // Left Leg
            this.bones.Add(new Tuple<JointType, JointType>(JointType.HipLeft, JointType.KneeLeft));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.KneeLeft, JointType.AnkleLeft));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.AnkleLeft, JointType.FootLeft));

            // populate body colors, one for each BodyIndex
            this.bodyColors = new List<Pen>();

            this.bodyColors.Add(new Pen(Brushes.Red, 6));
            this.bodyColors.Add(new Pen(Brushes.Orange, 6));
            this.bodyColors.Add(new Pen(Brushes.Green, 6));
            this.bodyColors.Add(new Pen(Brushes.Blue, 6));
            this.bodyColors.Add(new Pen(Brushes.Indigo, 6));
            this.bodyColors.Add(new Pen(Brushes.Violet, 6));

            // set IsAvailableChanged event notifier
            this.kinectSensor.IsAvailableChanged += this.Sensor_IsAvailableChanged;

            // open the sensor
            this.kinectSensor.Open();

            // set the status text
            this.StatusText = this.kinectSensor.IsAvailable ? Properties.Resources.RunningStatusText
                                                            : Properties.Resources.NoSensorStatusText;

            // Create the drawing group we'll use for drawing
            this.drawingGroup = new DrawingGroup();

            // Create an image source that we can use in our image control
            this.imageSource = new DrawingImage(this.drawingGroup);

            // use the window object as the view model in this simple example
            this.DataContext = this;

            // initialize the components (controls) of the window
            this.InitializeComponent();

            //Create radius
            this.DartBoardRadius[0] = 0.5;
            this.DartBoardRadius[1] = 1;
            this.DartBoardRadius[2] = 1.5;
            this.DartBoardRadius[3] = 2;
            this.DartBoardRadius[4] = 2.5;

            //Create center
            DartBoardCenter.Position.X = 0;
            DartBoardCenter.Position.Y = 0;
            DartBoardCenter.Position.Z = 20;
            
            //Initialize initshot
            initShot.Position.X = 0;
            initShot.Position.Y = 0;
            initShot.Position.Z = 0;
            finalShot.Position.X = 0;
            finalShot.Position.Y = 0;
            finalShot.Position.Z = 0;

            //Initialize time different to 0
            time = 10;

        }

        /// <summary>
        /// INotifyPropertyChangedPropertyChanged event to allow window controls to bind to changeable data
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Gets the bitmap to display
        /// </summary>
        public ImageSource ImageSource
        {
            get
            {
                return this.imageSource;
            }
        }

        /// <summary>
        /// Gets or sets the current status text to display
        /// </summary>
        public string StatusText
        {
            get
            {
                return this.statusText;
            }

            set
            {
                if (this.statusText != value)
                {
                    this.statusText = value;

                    // notify any bound elements that the text has changed
                    if (this.PropertyChanged != null)
                    {
                        this.PropertyChanged(this, new PropertyChangedEventArgs("StatusText"));
                    }
                }
            }
        }

        /// <summary>
        /// Execute start up tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (this.bodyFrameReader != null)
            {
                this.bodyFrameReader.FrameArrived += this.Reader_FrameArrived;
            }
        }

        /// <summary>
        /// Execute shutdown tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            if (this.bodyFrameReader != null)
            {
                // BodyFrameReader is IDisposable
                this.bodyFrameReader.Dispose();
                this.bodyFrameReader = null;
            }

            if (this.kinectSensor != null)
            {
                this.kinectSensor.Close();
                this.kinectSensor = null;
            }
        }

        /// <summary>
        /// Handles the body frame data arriving from the sensor
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void Reader_FrameArrived(object sender, BodyFrameArrivedEventArgs e)
        {
            bool dataReceived = false;
            bool shoot = false;

            using (BodyFrame bodyFrame = e.FrameReference.AcquireFrame())
            {
                if (bodyFrame != null)
                {
                    if (this.bodies == null)
                    {
                        this.bodies = new Body[bodyFrame.BodyCount];
                    }
                    // The first time GetAndRefreshBodyData is called, Kinect will allocate each Body in the array.
                    // As long as those body objects are not disposed and not set to null in the array,
                    // those body objects will be re-used.
                    bodyFrame.GetAndRefreshBodyData(this.bodies);
                    dataReceived = true;
                }
            }

            if (dataReceived)
            {
                using (DrawingContext dc = this.drawingGroup.Open())
                {
                    // Draw a transparent background to set the render size

                    System.String bgPath = Path.GetFullPath(@"..\..\..\Images\forest.jpg");
                    ImageBrush imageBg = new ImageBrush(new BitmapImage(new Uri(bgPath)));
                    dc.DrawRectangle(imageBg, null, new Rect(0.0, 0.0, this.displayWidth, this.displayHeight));
                    

                    int penIndex = 0;

                    // Flag to detect only one body
                    Boolean firstBody = false;
                    foreach (Body body in this.bodies)
                    {
                        Pen drawPen = this.bodyColors[penIndex++];

                        if (body.IsTracked && firstBody == false)
                        {
                            firstBody = true;

                            this.DrawClippedEdges(body, dc);

                            IReadOnlyDictionary<JointType, Joint> joints = body.Joints;

                            // convert the joint points to depth (display) space
                            Dictionary<JointType, Point> jointPoints = new Dictionary<JointType, Point>();

                            foreach (JointType jointType in joints.Keys)
                            {
                                // sometimes the depth(Z) of an inferred joint may show as negative
                                // clamp down to 0.1f to prevent coordinatemapper from returning (-Infinity, -Infinity)
                                CameraSpacePoint position = joints[jointType].Position;
                                if (position.Z < 0)
                                {
                                    position.Z = InferredZPositionClamp;
                                }

                                DepthSpacePoint depthSpacePoint = this.coordinateMapper.MapCameraPointToDepthSpace(position);
                                jointPoints[jointType] = new Point(depthSpacePoint.X, depthSpacePoint.Y);
                            }

                            JointType userHand = new JointType();
                            HandState userHandState = new HandState();

                            // While position is not correct, draw floor
                            if (HeadHeight == -10)
                            {
                                this.DrawBody(joints, jointPoints, dc, drawPen);
                                HeadHeight = this.DrawFloor(joints[JointType.FootLeft], joints[JointType.FootRight], joints[JointType.Head], dc);
                            }
                            else if (userSelectedHand == false)
                            {
                                string handsAdvice = "Elige diestro para tensar el arco con la mano derecha.\n"+
                                                     "Elige zurdo para tensar el arco con la mano izquierda.";
                                FormattedText floorAdviceText = new FormattedText(handsAdvice, CultureInfo.GetCultureInfo("en-us"),
                                FlowDirection.LeftToRight, new Typeface("Arial Black"), 17, Brushes.DarkBlue);
                                dc.DrawText(floorAdviceText, new Point(10, 20));
                                this.DrawBody(joints, jointPoints, dc, drawPen);
                                //User must select if he is left or right handed: Show buttons:
                                System.String leftPath = Path.GetFullPath(@"..\..\..\Images\leftHanded.png");
                                if (jointPoints[JointType.HandLeft].X > 100 && jointPoints[JointType.HandLeft].X < (100 + 100) &&
                                    jointPoints[JointType.HandLeft].Y > 150 && jointPoints[JointType.HandLeft].Y < 200)
                                {
                                    leftPath = Path.GetFullPath(@"..\..\..\Images\leftHanded2.png");
                                }
                                BitmapImage leftImage = new BitmapImage();
                                leftImage.BeginInit();
                                leftImage.UriSource = new Uri(leftPath);
                                leftImage.EndInit();
                                dc.DrawImage(leftImage, new Rect(100, 150, 100, 50));


                                System.String rightPath = Path.GetFullPath(@"..\..\..\Images\rightHanded.png");
                                if (jointPoints[JointType.HandRight].X > (displayWidth - 200) && jointPoints[JointType.HandRight].X < ((displayWidth - 200) + 100) &&
                                    jointPoints[JointType.HandRight].Y > 150 && jointPoints[JointType.HandRight].Y < 200)
                                {
                                    rightPath = Path.GetFullPath(@"..\..\..\Images\rightHanded2.png");
                                }
                                BitmapImage rightImage = new BitmapImage();
                                rightImage.BeginInit();
                                rightImage.UriSource = new Uri(rightPath);
                                rightImage.EndInit();
                                dc.DrawImage(rightImage, new Rect(displayWidth - 200, 150, 100, 50));

                                //Test if user closed his hand in a button:
                                //Left handed:
                                if (jointPoints[JointType.HandLeft].X > 100 && jointPoints[JointType.HandLeft].X < (100 + 100) &&
                                    jointPoints[JointType.HandLeft].Y > 150 && jointPoints[JointType.HandLeft].Y < 200 &&
                                    body.HandLeftState == HandState.Closed)
                                {
                                    userSelectedHand = true;
                                    rightHanded = false;
                                }
                                //Right handed:
                                else if (jointPoints[JointType.HandRight].X > (displayWidth - 200) && jointPoints[JointType.HandRight].X < ((displayWidth - 200) + 100) &&
                                    jointPoints[JointType.HandRight].Y > 150 && jointPoints[JointType.HandRight].Y < 200 &&
                                    body.HandRightState == HandState.Closed)
                                {
                                    userSelectedHand = true;
                                    rightHanded = true;
                                }
                            }
                            //User positioned and his hand is selected: Start the game
                            else if(time>0)
                            {
                                Joint shotImpactPoint = new Joint();
                                if (rightHanded)
                                {
                                    userHand = JointType.HandRight;
                                    userHandState = body.HandRightState;
                                }
                                else
                                {
                                    userHand = JointType.HandLeft;
                                    userHandState = body.HandLeftState;
                                }
                                if (userHandState == HandState.Closed && !HandClosed)
                                {
                                    HandClosed = true;
                                    initShot.Position.X = joints[userHand].Position.X;
                                    initShot.Position.Y = joints[userHand].Position.Y;
                                    initShot.Position.Z = joints[userHand].Position.Z;
                                    finalShot.Position.X = joints[userHand].Position.X;
                                    finalShot.Position.Y = joints[userHand].Position.Y;
                                    finalShot.Position.Z = joints[userHand].Position.Z;
                                }
                                else if (!HandClosed)
                                {
                                    initShot.Position.X = 0;
                                    initShot.Position.Y = 0;
                                    initShot.Position.Z = 0;
                                    finalShot.Position.X = 0;
                                    finalShot.Position.Y = 0;
                                    finalShot.Position.Z = 0;
                                }
                                else if (HandClosed && userHandState == HandState.Closed)
                                {
                                    finalShot.Position.X = joints[userHand].Position.X;
                                    finalShot.Position.Y = joints[userHand].Position.Y;
                                    finalShot.Position.Z = joints[userHand].Position.Z;
                                }
                                else if (HandClosed && userHandState == HandState.Open)
                                {
                                    HandClosed = false;
                                    shoot = true;
                                    finalShot.Position.X = joints[userHand].Position.X;
                                    finalShot.Position.Y = joints[userHand].Position.Y;
                                    finalShot.Position.Z = joints[userHand].Position.Z;
                                }
                                if (arrowInHand)
                                {
                                    //Show that user has the arrow
                                    System.String arrowPath = Path.GetFullPath(@"..\..\..\Images\arrow.png");
                                    BitmapImage arrowImage = new BitmapImage();
                                    arrowImage.BeginInit();
                                    arrowImage.UriSource = new Uri(arrowPath);
                                    arrowImage.EndInit();
                                    dc.DrawImage(arrowImage, new Rect(this.displayWidth - 80, 10, 80, 80));

                                    string showAdvice;
                                    if (rightHanded)
                                    {
                                        showAdvice = "Apunta con la mano izquierda.\n";
                                    }
                                    else
                                    {
                                        showAdvice = "Apunta con la mano derecha.\n";
                                    }
                                    
                                    if (initShot.Position.Z < finalShot.Position.Z)
                                    {
                                        if (rightHanded)
                                        {
                                            showAdvice = showAdvice + "Tensa la cuerda cerrando la mano\nderecha y llevándola hacia atrás.";
                                        }
                                        else
                                        {
                                            showAdvice = showAdvice + "Tensa la cuerda cerrando la mano\nizquierda y llevándola hacia atrás.";
                                        }
                                        if (shoot)
                                        {
                                            arrowInHand = false;
                                            //Calculate shot points:
                                            userTotalPuntuation += userPointsIfShots;
                                            shoot = false;
                                            //Sum up seconds depending on the puntuation
                                            currentMilliseconds += (long)userPointsIfShots * 1000 / 3;
                                        }
                                        else
                                        {
                                            shotImpactPoint = DrawDartBoard(initShot, finalShot, joints, dc);
                                            userPointsIfShots = computeShotPoints(shotImpactPoint);
                                        }
                                    }
                                    else
                                    {
                                        // If he charge forward or doesn't charge, draw initial dardboard saving the initial point
                                        DrawDartBoard(initShot, initShot, joints, dc);
                                    }
                                    //Advices to posicionate
                                    FormattedText floorAdviceText = new FormattedText(showAdvice, CultureInfo.GetCultureInfo("en-us"),
                                    FlowDirection.LeftToRight, new Typeface("Arial Black"), 25, Brushes.DarkBlue);
                                    dc.DrawText(floorAdviceText, new Point(20, this.displayHeight - 100));
                                }
                                else if (!arrowInHand)
                                {
                                    string arrowAdvice = "¡Coge una flecha!";
                                    FormattedText floorAdviceText = new FormattedText(arrowAdvice, CultureInfo.GetCultureInfo("en-us"),
                                    FlowDirection.LeftToRight, new Typeface("Arial Black"), 30, Brushes.DarkBlue);
                                    dc.DrawText(floorAdviceText, new Point(20, this.displayHeight - 120));
                                    //Show that user has NOT the arrow
                                    System.String noArrowPath = Path.GetFullPath(@"..\..\..\Images\noArrow.png");
                                    BitmapImage noArrowImage = new BitmapImage();
                                    noArrowImage.BeginInit();
                                    noArrowImage.UriSource = new Uri(noArrowPath);
                                    noArrowImage.EndInit();
                                    dc.DrawImage(noArrowImage, new Rect(this.displayWidth - 80, 10, 80, 80));

                                    //User has to take an arrow to shot it
                                    arrowInHand = GetArrow(joints[userHand], joints[JointType.Neck]);

                                    DrawDartBoard(initShot, initShot, joints, dc);
                                }

                            }
                            // If time is over
                            else
                            {
                                //Showing TIME OVER
                                FormattedText timeOverText = new FormattedText("TIME OVER", CultureInfo.GetCultureInfo("en-us"),
                                FlowDirection.LeftToRight, new Typeface("Arial Black"), 65, Brushes.Red);
                                dc.DrawText(timeOverText, new Point(60, 300));

                                //Showing user points:
                                FormattedText userPointsText = new FormattedText("Puntuación Final: " + userTotalPuntuation, CultureInfo.GetCultureInfo("en-us"),
                                FlowDirection.LeftToRight, new Typeface("Arial Black"), 36, Brushes.DarkBlue);
                                dc.DrawText(userPointsText, new Point(70, 250));

                                this.DrawBody(joints, jointPoints, dc, drawPen);
                                //User must select if he is left or right handed: Show buttons:
                                System.String restartPath = Path.GetFullPath(@"..\..\..\Images\restart.png");
                                if ((jointPoints[JointType.HandRight].X > 160 && jointPoints[JointType.HandRight].X < (160 + 150) &&
                                    jointPoints[JointType.HandRight].Y > 50 && jointPoints[JointType.HandRight].Y < 150) ||
                                    (jointPoints[JointType.HandLeft].X > 160 && jointPoints[JointType.HandLeft].X < (160 + 150) &&
                                    jointPoints[JointType.HandLeft].Y > 50 && jointPoints[JointType.HandLeft].Y < 150 ))
                                {
                                    restartPath = Path.GetFullPath(@"..\..\..\Images\restart2.png");
                                }
                                BitmapImage restartImage = new BitmapImage();
                                restartImage.BeginInit();
                                restartImage.UriSource = new Uri(restartPath);
                                restartImage.EndInit();
                                dc.DrawImage(restartImage, new Rect(160, 50, 150, 100));

                                //Test if user closed his hand in a button:
                                if ((jointPoints[JointType.HandRight].X > 160 && jointPoints[JointType.HandRight].X < (160 + 150) &&
                                    jointPoints[JointType.HandRight].Y > 50 && jointPoints[JointType.HandRight].Y < 150 &&
                                    body.HandRightState == HandState.Closed) ||
                                    (jointPoints[JointType.HandLeft].X > 160 && jointPoints[JointType.HandLeft].X < (160 + 150) &&
                                    jointPoints[JointType.HandLeft].Y > 50 && jointPoints[JointType.HandLeft].Y < 150 &&
                                    body.HandLeftState == HandState.Closed))
                                {
                                    //Restart the variables
                                    activateTime = true;
                                    userSelectedHand = false;
                                    arrowInHand = false;
                                    userTotalPuntuation = 0;
                                    time = 10;
                                }
                            }
                        }
                    }

                    // prevent drawing outside of our render area
                    this.drawingGroup.ClipGeometry = new RectangleGeometry(new Rect(0.0, 0.0, this.displayWidth, this.displayHeight));
                }
            }
        }

        /// <summary>
        /// Draws a body
        /// </summary>
        /// <param name="joints">joints to draw</param>
        /// <param name="jointPoints">translated positions of joints to draw</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        /// <param name="drawingPen">specifies color to draw a specific body</param>
        private void DrawBody(IReadOnlyDictionary<JointType, Joint> joints, IDictionary<JointType, Point> jointPoints, DrawingContext drawingContext, Pen drawingPen)
        {
            // Draw the bones
            foreach (var bone in this.bones)
            {
                this.DrawBone(joints, jointPoints, bone.Item1, bone.Item2, drawingContext, drawingPen);
            }

            // Draw the joints
            foreach (JointType jointType in joints.Keys)
            {
                Brush drawBrush = null;

                TrackingState trackingState = joints[jointType].TrackingState;

                if (trackingState == TrackingState.Tracked)
                {
                    drawBrush = this.trackedJointBrush;
                }
                else if (trackingState == TrackingState.Inferred)
                {
                    drawBrush = this.inferredJointBrush;
                }

                if (drawBrush != null)
                {
                    drawingContext.DrawEllipse(drawBrush, null, jointPoints[jointType], JointThickness, JointThickness);
                }
            }
        }

        /// <summary>
        /// Draws one bone of a body (joint to joint)
        /// </summary>
        /// <param name="joints">joints to draw</param>
        /// <param name="jointPoints">translated positions of joints to draw</param>
        /// <param name="jointType0">first joint of bone to draw</param>
        /// <param name="jointType1">second joint of bone to draw</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        /// /// <param name="drawingPen">specifies color to draw a specific bone</param>
        private void DrawBone(IReadOnlyDictionary<JointType, Joint> joints, IDictionary<JointType, Point> jointPoints, JointType jointType0, JointType jointType1, DrawingContext drawingContext, Pen drawingPen)
        {
            Joint joint0 = joints[jointType0];
            Joint joint1 = joints[jointType1];

            // If we can't find either of these joints, exit
            if (joint0.TrackingState == TrackingState.NotTracked ||
                joint1.TrackingState == TrackingState.NotTracked)
            {
                return;
            }

            // We assume all drawn bones are inferred unless BOTH joints are tracked
            Pen drawPen = this.inferredBonePen;
            if ((joint0.TrackingState == TrackingState.Tracked) && (joint1.TrackingState == TrackingState.Tracked))
            {
                drawPen = drawingPen;
            }

            drawingContext.DrawLine(drawPen, jointPoints[jointType0], jointPoints[jointType1]);
        }

        /// <summary>
        /// Draws a hand symbol if the hand is tracked: red circle = closed, green circle = opened; blue circle = lasso
        /// </summary>
        /// <param name="handState">state of the hand</param>
        /// <param name="handPosition">position of the hand</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        private void DrawHand(HandState handState, Point handPosition, float z, DrawingContext drawingContext)
        {
            double ellipseSize = 2.0 / z;
            switch (handState)
            {
                case HandState.Closed:
                    drawingContext.DrawEllipse(this.handClosedBrush, null, handPosition, ellipseSize * HandSize, ellipseSize * HandSize);
                    break;

                case HandState.Open:
                    drawingContext.DrawEllipse(this.handOpenBrush, null, handPosition, ellipseSize * HandSize, ellipseSize * HandSize);
                    break;

                case HandState.Lasso:
                    drawingContext.DrawEllipse(this.handLassoBrush, null, handPosition, ellipseSize * HandSize, ellipseSize * HandSize);
                    break;
            }
        }


        /// <summary>
        /// Recognise if user takes an arrow
        /// </summary>
        /// <param name="userHand">user hand</param>
        /// <param name="userNeck">user neck</param>
        bool GetArrow(Joint userHand, Joint userNeck)
        {
            //Gesture to take a arrow
            if(userHand.Position.Z >= userNeck.Position.Z && userHand.Position.Y >= userNeck.Position.Y)
            {
                return true;
            }
            else
            {
                return false;
            }
        }


        /// <summary>
        /// Calculate points of the shot
        /// </summary>
        /// <param name="shot">Joint where shot impacted</param>
        int computeShotPoints(Joint shot)
        {
            //Calculate module from shot to center
            double module = Math.Sqrt((shot.Position.X - DartBoardCenter.Position.X) * (shot.Position.X - DartBoardCenter.Position.X) +
            (shot.Position.Y - DartBoardCenter.Position.Y) * (shot.Position.Y - DartBoardCenter.Position.Y));
            //Return points:
            if (module < 0.5)
            {
                return 10;
            }
            else if(module < 1)
            {
                return 5;
            }
            else if (module < 1.5)
            {
                return 3;
            }
            else if (module < 2)
            {
                return 2;
            }
            else if (module < 2.5)
            {
                return 1;
            }
            else
            {
                return -5;
            }
        }


        /// <summary>
        /// Draws an ellipse in the posture goal
        /// </summary>
        /// <param name="goal">position of the hand</param>
        /// <param name="hand">position of the hand</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        private Joint DrawDartBoard(Joint initialHand, Joint currentHand, IReadOnlyDictionary<JointType, Joint> joints, DrawingContext drawingContext)
        {

            if (activateTime)
            {
                //Initial milliseconds, 30 seconds from now
                currentMilliseconds = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond + (long)30000;
                activateTime = false;
            }

            //Angles of 3D parabolic movement
            float alpha;
            float beta;
            
            //Point where arrow will impact if user throws it
            Joint impactPoint = new Joint();

            // Convert 3D to 2D
            DepthSpacePoint depth_center = this.coordinateMapper.MapCameraPointToDepthSpace(DartBoardCenter.Position);
            Point center_2D = new Point(depth_center.X, depth_center.Y);

            //Control the countdown
            long milliseconds = currentMilliseconds - DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;

            time = (float) milliseconds / 1000;

            //Size of the board
            double factor = 400.0 / DartBoardCenter.Position.Z;

            //Draw the dartboard
            drawingContext.DrawEllipse(new SolidColorBrush(Color.FromArgb(255, 255, 0, 0)), null, center_2D, DartBoardRadius[4] * factor, DartBoardRadius[4] * factor);
            drawingContext.DrawEllipse(new SolidColorBrush(Color.FromArgb(255, 255, 255, 255)), null, center_2D, DartBoardRadius[3] * factor, DartBoardRadius[3] * factor);
            drawingContext.DrawEllipse(new SolidColorBrush(Color.FromArgb(255, 255, 0, 0)), null, center_2D, DartBoardRadius[2] * factor, DartBoardRadius[2] * factor);
            drawingContext.DrawEllipse(new SolidColorBrush(Color.FromArgb(255, 255, 255, 255)), null, center_2D, DartBoardRadius[1] * factor, DartBoardRadius[1] * factor);
            drawingContext.DrawEllipse(new SolidColorBrush(Color.FromArgb(255, 255, 0, 0)), null, center_2D, DartBoardRadius[0] * factor, DartBoardRadius[0] * factor);


            string timeStr = time.ToString("#.#");

            //Showing user points:
            FormattedText userPointsText = new FormattedText("Puntos: " + userTotalPuntuation, CultureInfo.GetCultureInfo("en-us"),
            FlowDirection.LeftToRight, new Typeface("Arial Black"), 28, Brushes.Black);
            drawingContext.DrawText(userPointsText, new Point(230, 10));

            //Showing time:
            FormattedText timeText = new FormattedText("Tiempo: " + timeStr, CultureInfo.GetCultureInfo("en-us"),
            FlowDirection.LeftToRight, new Typeface("Arial Black"), 28, Brushes.Black);
            drawingContext.DrawText(timeText, new Point(20, 10));

            //If user is charging, draw estimated point
            if(initialHand.Position.Z < currentHand.Position.Z){

                //Force of the shot
                float power = (float)currentHand.Position.Z - initialHand.Position.Z;
                
                //Control the arm we aim
                if (!rightHanded)
                {
                    // alpha angle
                    alpha = (float)Math.Atan2(joints[JointType.HandRight].Position.Y - joints[JointType.ShoulderRight].Position.Y,
                                    joints[JointType.ShoulderRight].Position.Z - joints[JointType.HandRight].Position.Z);
                    // beta angle
                    beta = (float)Math.Atan2(joints[JointType.HandRight].Position.X - joints[JointType.ShoulderRight].Position.X,
                                        joints[JointType.ShoulderRight].Position.Z - joints[JointType.HandRight].Position.Z);
                }
                else
                {
                    // alpha angle
                    alpha = (float)Math.Atan2(joints[JointType.HandLeft].Position.Y - joints[JointType.ShoulderLeft].Position.Y,
                                    joints[JointType.ShoulderLeft].Position.Z - joints[JointType.HandLeft].Position.Z);
                    // beta angle
                    beta = (float)Math.Atan2(joints[JointType.HandLeft].Position.X - joints[JointType.ShoulderLeft].Position.X,
                                        joints[JointType.ShoulderLeft].Position.Z - joints[JointType.HandLeft].Position.Z);
                }
                
                double g = 9.8; //Superficial gravity field
                double y0 = initialHand.Position.Y + 1; //Initial height
                double v0 = 47 * power; //Initial velocity, depending on the power 
                double z = DartBoardCenter.Position.Z; //Distance of the dartboard
                
                //Parabolic movement equations, the intersection between X and Y flat
                impactPoint.Position.X = (float)(z * Math.Tan(beta));
                impactPoint.Position.Y = (float)(y0 + z * Math.Tan(alpha) / Math.Cos(beta) - 0.5 * g * z * z / (v0 * v0 * Math.Cos(alpha) * Math.Cos(alpha) * Math.Cos(beta) * Math.Cos(beta)));
                impactPoint.Position.Z = (float)z;


                // Convert 3D to 2D
                DepthSpacePoint depth_impact = this.coordinateMapper.MapCameraPointToDepthSpace(impactPoint.Position);
                Point impact_2D = new Point(depth_impact.X, depth_impact.Y);
                
                //If user has no arrow, do not draw impact point:
                if (arrowInHand)
                {
                    //Show that user has the arrow
                    System.String arrowPath = Path.GetFullPath(@"..\..\..\Images\mirilla.png");
                    BitmapImage arrowImage = new BitmapImage();
                    arrowImage.BeginInit();
                    arrowImage.UriSource = new Uri(arrowPath);
                    arrowImage.EndInit();

                    //Size of the eyehole
                    float sizeEyeHole =  25 / power;
                    drawingContext.DrawImage(arrowImage, new Rect(impact_2D.X - sizeEyeHole / 2, impact_2D.Y - sizeEyeHole / 2, sizeEyeHole, sizeEyeHole));

                    //Draw lines representating power:
                    Point power_start_2D = new Point(10, 200);
                    Point power_end_2D = new Point(10, 200 - power * 200);
                    byte redPower = (byte)(255 - power * 200);
                    if (redPower < 0) 
                    {
                        redPower = 0;
                    }
                    Pen line_pen = new Pen(new SolidColorBrush(Color.FromArgb(255, 255, redPower, 0)), 10);
                    drawingContext.DrawLine(line_pen, power_start_2D, power_end_2D);
                }
            }

            return impactPoint;

        }



        /// <summary>
        /// Draws an ellipse in the floor goal
        /// </summary>
        /// <param name="foot_left">position of the left foot</param>
        /// /// <param name="foot_right">position of the right foot</param>
        /// <param name="head">position of the head</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        private double DrawFloor(Joint foot_left, Joint foot_right, Joint head, DrawingContext drawingContext)
        {

            Joint center_floor = new Joint();
            center_floor.Position.X = (float)FloorCenterX;
            center_floor.Position.Y = (float)FloorCenterY;
            center_floor.Position.Z = (float)FloorCenterZ;

            // Transform world coordinates to screen coordinates
            DepthSpacePoint depth_center_floor = this.coordinateMapper.MapCameraPointToDepthSpace(center_floor.Position);
            Point center_floor_2D = new Point(depth_center_floor.X, depth_center_floor.Y);

            // If the user is in the good floor position, the ellipse change it color to green
            if ((foot_left.Position.X < FloorCenterX + 0.3 && foot_left.Position.X > FloorCenterX - 0.3) &&
                (foot_right.Position.X < FloorCenterX + 0.3 && foot_right.Position.X > FloorCenterX - 0.3) &&
                (foot_left.Position.Y < FloorCenterY + 0.3 && foot_left.Position.Y > FloorCenterY - 0.3) &&
                (foot_right.Position.Y < FloorCenterY + 0.3 && foot_right.Position.Y > FloorCenterY - 0.3) &&
                (foot_left.Position.Z < FloorCenterZ + 0.3 && foot_left.Position.Z > FloorCenterZ - 0.3) &&
                (foot_right.Position.Z < FloorCenterZ + 0.3 && foot_right.Position.Z > FloorCenterZ - 0.3))
            {
                drawingContext.DrawEllipse(Brushes.Red, null, center_floor_2D, 24, 8);
                // Return the user's height
                return head.Position.Y;
            }
            // If not, the ellipse change it color to red
            else
            {
                //Showing advices to positioning
                string floorAdvice = "";
                if (head.Position.X < FloorCenterX )
                {
                    if (head.Position.Z < FloorCenterZ)
                    {
                        floorAdvice = "Muévete hacia la derecha y \n hacia atrás";
                    }
                    else if (head.Position.Z > FloorCenterZ)
                    {
                        floorAdvice = "Muévete hacia la derecha y \n hacia delante";
                    }
                }
                else if (head.Position.X > FloorCenterX)
                {
                    if (head.Position.Z < FloorCenterZ)
                    {
                        floorAdvice = "Muévete hacia la izquierda y \n hacia atrás";
                    }
                    else if (head.Position.Z > FloorCenterZ)
                    {
                        floorAdvice = "Muévete hacia la izquierda y \n hacia delante";
                    }
                }

                //Draw on the screen
                FormattedText floorAdviceText = new FormattedText(floorAdvice, CultureInfo.GetCultureInfo("en-us"),
                FlowDirection.LeftToRight, new Typeface("Arial Black"), 22, Brushes.DarkBlue);
                drawingContext.DrawText(floorAdviceText, new Point(90, 20));

                //Draw floor ellipse
                drawingContext.DrawEllipse(Brushes.Red, null, center_floor_2D, 50, 8);
            }
            // When the user is in a bad floor position, return -10
            return -10;

        }

        /// <summary>
        /// Draws indicators to show which edges are clipping body data
        /// </summary>
        /// <param name="body">body to draw clipping information for</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        private void DrawClippedEdges(Body body, DrawingContext drawingContext)
        {
            FrameEdges clippedEdges = body.ClippedEdges;

            if (clippedEdges.HasFlag(FrameEdges.Bottom))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, this.displayHeight - ClipBoundsThickness, this.displayWidth, ClipBoundsThickness));
            }

            if (clippedEdges.HasFlag(FrameEdges.Top))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, 0, this.displayWidth, ClipBoundsThickness));
            }

            if (clippedEdges.HasFlag(FrameEdges.Left))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, 0, ClipBoundsThickness, this.displayHeight));
            }

            if (clippedEdges.HasFlag(FrameEdges.Right))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(this.displayWidth - ClipBoundsThickness, 0, ClipBoundsThickness, this.displayHeight));
            }
        }

        /// <summary>
        /// Handles the event which the sensor becomes unavailable (E.g. paused, closed, unplugged).
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void Sensor_IsAvailableChanged(object sender, IsAvailableChangedEventArgs e)
        {
            // on failure, set the status text
            this.StatusText = this.kinectSensor.IsAvailable ? Properties.Resources.RunningStatusText
                                                            : Properties.Resources.SensorNotAvailableStatusText;
        }

        
    }
}
