using System.IO;
using System.Reactive.Disposables;
using Emgu.CV.Structure;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Emgu.CV.Util;
using System.Drawing;
using System.Collections.Generic;
namespace EyeAuras.Web.Repl.Component;

public partial class Main : WebUIComponent
{
    public Main()
    {
    }

    private IImageSearchTrigger Minimap =>
        AuraTree.GetAuraByPath(@".\Minimap").Triggers.Items.OfType<IImageSearchTrigger>().First();

    private ISendInputAction SendInput =>
        AuraTree.GetAuraByPath(@".\ClickMove").WhileActiveActions.Items.OfType<ISendInputAction>().First();

    private IImageSearchTrigger PlayerHP =>
        AuraTree.GetAuraByPath(@".\PlayerHP").Triggers.Items.OfType<IImageSearchTrigger>().First();

    protected override async Task HandleAfterFirstRender()
    {
        Disposable.Create(() => Log.Info("Disposed")).AddTo(Anchors);

        Minimap.ImageSink.Where(_ => Enabled).Subscribe(x => 
            Task.Run(() => StartGetVal(x))).AddTo(Anchors);
    
        PlayerHP.ImageSink.Where(_ => Enabled).Subscribe(x => 
            Task.Run(() => HPBar(x))).AddTo(Anchors);
    }

    private bool Enabled { get; set; } = false;
    private string MyImageString { get; set; } = "data:image/jpeg;base64,YourBase64String";
    private bool TogglePreview { get; set; }

    private Rectangle Window { get; set; }
    private Rectangle CurrentBounds { get; set; }
    private Rectangle CenterCords { get; set; }

    private readonly object _getValLock = new object();
    private Task _currentGetValTask;

    private async Task StartGetVal(Image<Bgr, byte> inputImage)
    {
        var sw = new Stopwatch();
        sw.Start();
        await Task.Run(() => GetVal(inputImage));
        sw.Stop();
        Log.Info($"Full render - {sw.ElapsedMilliseconds}ms");
    }

    public double globalPercentage = 0;

    private async Task HPBar(Image<Bgr, byte> inputImage)
    {
        // Конвертируем изображение в черно-белое для удобства
        var grayImage = inputImage.Convert<Gray, byte>();

        // Находим все белые пиксели по горизонтали
        int firstWhitePixel = -1;
        int lastWhitePixel = -1;

        for (int x = 0; x < grayImage.Width; x++)
        {
            for (int y = 0; y < grayImage.Height; y++)
            {
                if (grayImage.Data[y, x, 0] > 128) // Пиксель является белым
                {
                    if (firstWhitePixel == -1)
                    {
                        firstWhitePixel = x;
                    }

                    lastWhitePixel = x;
                    break;
                }
            }
        }

        // Если белые пиксели были найдены, вычисляем процент
        if (firstWhitePixel != -1 && lastWhitePixel != -1)
        {
            int widthOfWhitePixels = lastWhitePixel - firstWhitePixel + 1;
            globalPercentage = (double)widthOfWhitePixels / 150 * 100;

            // Ограничиваем значение до 100%
            globalPercentage = Math.Min(globalPercentage, 100);
            await JSRuntime.InvokeVoidAsync("updateProgressBar", globalPercentage);
        }
        else
        {
            globalPercentage = 0;
        }
    }

    private int _cx = 107;
    private int _cy = 142;

    private async Task GetVal(Image<Bgr, byte> inputImage)
    {
        try
        {
            var windowBounds = Minimap.ActiveWindow.DwmWindowBounds;
            var currentBounds = Minimap.WindowRegion.Bounds.Bounds;
            var centerOfMinimap = new Rectangle(windowBounds.Width - _cx, _cy, 1, 1);
            var centerRelativeToImage = new Point(centerOfMinimap.X - currentBounds.X, centerOfMinimap.Y - currentBounds.Y);

            Image<Gray, byte> mask = inputImage.InRange(new Bgr(255, 255, 255), new Bgr(255, 255, 255));

            VectorOfVectorOfPoint contours = new VectorOfVectorOfPoint();
            Mat hierarchy = new Mat();

            CvInvoke.FindContours(mask, contours, hierarchy, Emgu.CV.CvEnum.RetrType.External, Emgu.CV.CvEnum.ChainApproxMethod.ChainApproxSimple);

            Point closestWhitePoint = FindClosestWhitePoint(contours, centerRelativeToImage);

            Point relativeToPoint = new Point(closestWhitePoint.X + currentBounds.X - windowBounds.X,
                closestWhitePoint.Y + currentBounds.Y - windowBounds.Y);

            ProcessDirectionAndClick(windowBounds, centerRelativeToImage, closestWhitePoint);

            if (TogglePreview)
            {
                GeneratePreview(inputImage, closestWhitePoint, centerRelativeToImage, contours);
            }
        }
        catch (Exception ex) // Consider catching specific exceptions if possible.
        {
            Log.Info($"Error details: {ex.ToString()}");
        }
    }

    private int IGNORE_DISTANCE = 15; // Глобальная переменная для задания расстояния игнорирования

    private Point FindClosestWhitePoint(VectorOfVectorOfPoint contours, Point centerRelativeToImage)
    {
        Point closestPoint = new Point();
        double minDistance = double.MaxValue;

        foreach (var contour in contours.ToArrayOfArray())
        {
            foreach (var pt in contour)
            {
                double distance = CalculateDistance(pt, centerRelativeToImage);
                if (distance >= IGNORE_DISTANCE && distance < minDistance)
                {
                    minDistance = distance;
                    closestPoint = pt;
                }
            }
        }

        return closestPoint;
    }

    private double CalculateDistance(Point p1, Point p2)
    {
        double dx = p1.X - p2.X;
        double dy = p1.Y - p2.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }


    private int rad = 250;

    private void ProcessDirectionAndClick(Rectangle windowBounds, Point centerRelativeToImage, Point closestPoint)
    {
        double directionX = closestPoint.X - centerRelativeToImage.X;
        double directionY = closestPoint.Y - centerRelativeToImage.Y;


        double magnitude = CalculateDistance(new Point(0, 0), new Point((int)directionX, (int)directionY));
        directionX /= magnitude;
        directionY /= magnitude;


        var characterCenter = new Point(windowBounds.Width / 2, (int)(windowBounds.Height * 0.53));
        

        int clickX = characterCenter.X + (int)(directionX * rad);
        int clickY = characterCenter.Y + (int)(directionY * rad);

        
        if (clickX >= 0 && clickY >= 0)
        {
            SendInput.MousePosition.SourceBounds.Location = new Point(clickX, clickY);
        }
        else
        {
            Log.Info("Negative click coordinate detected.");
        }
    }

    
    private void GeneratePreview(Image<Bgr, byte> inputImage, Point closestPoint, Point centerCordsRelativeToInputImage,
        VectorOfVectorOfPoint contours)
    {
        int size = 1;
        int proximityThreshold = 111;
        // Удвоим размер изображения, например, в 2 раза
        Image<Bgr, byte> enlargedImage = inputImage.Resize(size, Emgu.CV.CvEnum.Inter.Linear);

        // Теперь отмасштабируем координаты точек для увеличенного изображения
        closestPoint = new Point(closestPoint.X * size, closestPoint.Y * size);
        centerCordsRelativeToInputImage =
            new Point(centerCordsRelativeToInputImage.X * size, centerCordsRelativeToInputImage.Y * size);

        List<Point> existingPoints = new List<Point>(); 

        for (int i = 0; i < contours.Size; i++)
        {
            for (int j = 0; j < contours[i].Size; j++)
            {
                Point pt = new Point(contours[i][j].X * 2, contours[i][j].Y * 2);
                
                
            }
        }

        enlargedImage.Draw(new CircleF(closestPoint, 2), new Bgr(0, 0, 255), 2);
        enlargedImage.Draw(
            new CircleF(new PointF(centerCordsRelativeToInputImage.X, centerCordsRelativeToInputImage.Y), 2),
            new Bgr(0, 255, 0), 2);

        // Рассчитываем координаты для стрелки с отступом
        Point start = centerCordsRelativeToInputImage;
        Point end = closestPoint;
        double angle = Math.Atan2(end.Y - start.Y, end.X - start.X);
        start.X += (int)(5 * Math.Cos(angle));
        start.Y += (int)(5 * Math.Sin(angle));
        end.X -= (int)(5 * Math.Cos(angle));
        end.Y -= (int)(5 * Math.Sin(angle));

        // Рисование стрелки
        MCvScalar arrowColor = new Bgr(0, 0, 255).MCvScalar; // Цвет стрелки (красный)
        CvInvoke.ArrowedLine(enlargedImage, start, end, arrowColor, 2, Emgu.CV.CvEnum.LineType.AntiAlias);

        using (MemoryStream memory = new MemoryStream())
        {
            enlargedImage.ToBitmap().Save(memory, System.Drawing.Imaging.ImageFormat.Jpeg);
            byte[] imageBytes = memory.ToArray();
            base64ImageString = "data:image/jpeg;base64," + Convert.ToBase64String(imageBytes);

            JSRuntime.InvokeVoidAsync("updateImage", base64ImageString);
        }
        
        
    }

    private string base64ImageString;
}