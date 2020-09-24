﻿using AForge.Video;
using AForge.Video.DirectShow;
using Microsoft.ML;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Traning.MachineLearning.ObjectDetection.Builder;
using Traning.MachineLearning.ObjectDetection.Data;
using Traning.MachineLearning.ObjectDetection.Helpers;

namespace Traning.MachineLearning.ObjectDetection
{
    public partial class MainWindow : Window
    {
        private VideoCaptureDevice _video1;
        private VideoCaptureDevice _video2;
        private VideoCaptureDevice _video3;
        private PredictionEngine<HumanData, HumanPrediction> _predictionEngine1;
        private PredictionEngine<ObjectData, ObjectDetectionPrediction> _predictionEngine3;
        private string _imagesFolder1 = @"h:\data\human-detection";

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Train1()
        {
            var tfm = @"h:\data\models\tensorflow_inception_graph.pb";
            var mlContext = new MLContext();
            var data = mlContext.Data.LoadFromTextFile<HumanData>($"{_imagesFolder1}\\data.csv", separatorChar: ',');
            var split = mlContext.Data.TrainTestSplit(data, 0.8);
            var pipe = mlContext.Transforms.LoadImages("Image", _imagesFolder1, "Path")
                .Append(mlContext.Transforms.ResizeImages("ImageResized", 244, 244, "Image"))
                .Append(mlContext.Transforms.ExtractPixels("input", "ImageResized", interleavePixelColors: true))
                .Append(mlContext.Model.LoadTensorFlowModel(tfm).ScoreTensorFlowModel("softmax1_pre_activation", "input", true))
                .Append(mlContext.BinaryClassification.Trainers.LbfgsLogisticRegression(labelColumnName: "Label", featureColumnName: "softmax1_pre_activation"));

            var stopwatch = new Stopwatch();
            stopwatch.Start();
            var model = pipe.Fit(split.TrainSet);
            var test = model.Transform(split.TestSet);
            stopwatch.Stop();
            var metrics = mlContext.BinaryClassification.Evaluate(test, "Label");
            Dispatcher.Invoke(() =>
            {
                Info1.Content = $"Traning done. Accuracy: {metrics.Accuracy:P2}, Time: {stopwatch.Elapsed}";
            });
            _predictionEngine1 = mlContext.Model.CreatePredictionEngine<HumanData, HumanPrediction>(model);
        }

        private void T1_Start_Button_Click(object sender, RoutedEventArgs e)
        {
            if (_predictionEngine1 == null)
            {
                Task.Run(Train1);
            }

            var filterInfoCollection = new FilterInfoCollection(FilterCategory.VideoInputDevice);
            _video1 = new VideoCaptureDevice(filterInfoCollection[0].MonikerString);
            _video1.NewFrame += _video1_NewFrame;
            _video1.Start();
        }

        private void T1_Stop_Button_Click(object sender, RoutedEventArgs e)
        {
            _video1?.SignalToStop();
            Image1.Source = null;
            _predictionEngine1 = null;
        }

        private void _video1_NewFrame(object sender, NewFrameEventArgs eventArgs)
        {
            var path = $"{_imagesFolder1}\\temp.png";
            eventArgs.Frame.Save(path, ImageFormat.Png);
            if (_predictionEngine1 != null)
            {
                var prediction = _predictionEngine1.Predict(new HumanData { Path = path });
                using (var gr = Graphics.FromImage(eventArgs.Frame))
                {
                    gr.SmoothingMode = SmoothingMode.AntiAlias;
                    using (var thick_pen = new Pen(prediction.Prediction ? Color.Green : Color.Red, 5))
                    {
                        gr.DrawRectangle(thick_pen, 0, 0, 640, 480);
                    }
                }
            }
            Dispatcher.Invoke(() =>
            {
                Image1.Source = eventArgs.Frame.ToBitmapImage();
            });
        }

        private void T2_Start_Button_Click(object sender, RoutedEventArgs e)
        {
            var filterInfoCollection = new FilterInfoCollection(FilterCategory.VideoInputDevice);
            _video2 = new VideoCaptureDevice(filterInfoCollection[0].MonikerString);
            _video2.NewFrame += _video2_NewFrame;
            _video2.Start();
        }

        private void T2_Stop_Button_Click(object sender, RoutedEventArgs e)
        {
            _video2?.SignalToStop();
            Image2.Source = null;
        }

        private void _video2_NewFrame(object sender, NewFrameEventArgs eventArgs)
        {
            var path = $"{_imagesFolder1}\\temp.png";
            eventArgs.Frame.Save(path, ImageFormat.Png);
            var prediction = ConsumeModel.Predict(new ModelInput { ImageSource = path });
            using (var gr = Graphics.FromImage(eventArgs.Frame))
            {
                gr.SmoothingMode = SmoothingMode.AntiAlias;
                using (var thick_pen = new Pen(prediction.Prediction == "1" ? Color.Green : Color.Red, 5))
                {
                    gr.DrawRectangle(thick_pen, 0, 0, 640, 480);
                }
            }
            Dispatcher.Invoke(() =>
            {
                Info2.Content = $"Score: {prediction.Score[1]:P2}";
                Image2.Source = eventArgs.Frame.ToBitmapImage();
            });
        }

        private void T3_Start_Button_Click(object sender, RoutedEventArgs e)
        {
            var tfm = @"h:\data\models\tensorflow_inception_graph.pb";
            var mlContext = new MLContext();
            var pipe = mlContext.Transforms.LoadImages("Image", _imagesFolder1, "Path")
                .Append(mlContext.Transforms.ResizeImages("ImageResized", 244, 244, "Image"))
                .Append(mlContext.Transforms.ExtractPixels("input", "ImageResized", interleavePixelColors: true))
                .Append(mlContext.Model.LoadTensorFlowModel(tfm).ScoreTensorFlowModel("softmax2_pre_activation", "input", true))
                .Append(mlContext.Transforms.CopyColumns("Label", "softmax2_pre_activation"));

            var enumerableData = new List<ObjectData>();
            var data = mlContext.Data.LoadFromEnumerable(enumerableData);

            _predictionEngine3 = mlContext.Model.CreatePredictionEngine<ObjectData, ObjectDetectionPrediction>(pipe.Fit(data));

            var filterInfoCollection = new FilterInfoCollection(FilterCategory.VideoInputDevice);
            _video3 = new VideoCaptureDevice(filterInfoCollection[0].MonikerString);
            _video3.NewFrame += _video3_NewFrame;
            _video3.Start();
        }

        private void T3_Stop_Button_Click(object sender, RoutedEventArgs e)
        {
            _video3?.SignalToStop();
            Image3.Source = null;
        }

        private void _video3_NewFrame(object sender, NewFrameEventArgs eventArgs)
        {
            var path = $"{_imagesFolder1}\\temp.png";
            eventArgs.Frame.Save(path, ImageFormat.Png);
            if (_predictionEngine3 != null)
            {
                var prediction = _predictionEngine3.Predict(new ObjectData { Path = path });
                Dispatcher.Invoke(() =>
                {
                    Info3.Content = prediction.Prediction;
                });
            }
            Dispatcher.Invoke(() =>
            {
                Image3.Source = eventArgs.Frame.ToBitmapImage();
            });
        }
    }
}