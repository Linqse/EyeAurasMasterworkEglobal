using System.IO;
using System.Reactive.Disposables;
using Emgu.CV.Structure;
using Emgu.CV;

using Emgu.CV.Util;
using System.Collections.Generic;
using System.Reactive;
using System.Text;
using EyeAuras.OpenCVAuras.Scaffolding;
using EyeAuras.Roxy.Shared.Actions.SendInput;
using PoeShared.UI;

namespace EyeAuras.Web.Repl.Component;

public partial class Main : WebUIComponent
{
    [Dependency] public ISendInputController SendInputController { get; init; }
    
    [Dependency] public IHotkeyConverter HotkeyConverter { get; init; }
    [Dependency] public IUsernameProvider UsernameProvider { get; init; }
    public Main()
    {
    }
    
    private static readonly SendInputArgs DefaultSendInputArgs = new()
    {
        MinDelay = TimeSpan.FromMilliseconds(25),
        MaxDelay = TimeSpan.FromMilliseconds(35),
        InputSimulatorId = "TetherScriptFree",
        InputEventType = InputEventType.KeyPress
    };
    private IImageSearchTrigger Minimap =>
        AuraTree.GetAuraByPath(@".\Search\Minimap").Triggers.Items.OfType<IImageSearchTrigger>().First();
    
  
    private IImageSearchTrigger PlayerHp =>
        AuraTree.GetAuraByPath(@".\Search\PlayerHP").Triggers.Items.OfType<IImageSearchTrigger>().First();

    private IHotkeyIsActiveTrigger Hotkey =>
        AuraTree.GetAuraByPath(@".\StartKey").Triggers.Items.OfType<IHotkeyIsActiveTrigger>().First();

    private IImageSearchTrigger TargetHp => AuraTree.GetAuraByPath(@".\Search\TargetHP").Triggers.Items
        .OfType<IImageSearchTrigger>().First();
    private IImageSearchTrigger Spoiled => AuraTree.GetAuraByPath(@".\Search\Spoiled").Triggers.Items
        .OfType<IImageSearchTrigger>().First();

    private IImageSearchTrigger Coctail => AuraTree.GetAuraByPath(@".\Search\Coctail").Triggers.Items
        .OfType<IImageSearchTrigger>().First();

    private ITextSearchTrigger IgnorTarget => AuraTree.GetAuraByPath(@".\Search\IgnorTarget").Triggers.Items
        .OfType<ITextSearchTrigger>().First();
    private IImageSearchTrigger CharacterDead => AuraTree.GetAuraByPath(@".\Search\Dead").Triggers.Items
        .OfType<IImageSearchTrigger>().First();

    private ISendToTelegramAction TelegramAction => AuraTree.GetAuraByPath(@".\SendToTelegram").OnEnterActions.Items
        .OfType<ISendToTelegramAction>().First();
    

    private IDefaultTrigger Attack =>
        AuraTree.GetAuraByPath(@".\Attack").Triggers.Items.OfType<IDefaultTrigger>().First();

    private IWebUIAuraOverlay Overlay => AuraTree.Aura.Overlays.Items.OfType<IWebUIAuraOverlay>().First();

    private IHotkeyIsActiveTrigger OverlayHotkey =>
        AuraTree.Aura.Triggers.Items.OfType<IHotkeyIsActiveTrigger>().First();

    private void StartParam()
    {
        try
        {
            var dwm = Minimap.ActiveWindow.DwmWindowBounds;
            AuraTree.Aura["TargetHP"] = new Rectangle(dwm.Width / 2 - 150, 0, 100, 70);
            AuraTree.Aura["IgnorTarget"] = new Rectangle(dwm.Width / 2 - 50, 0, 300, 70);
            AuraTree.Aura["Center"] = new Rectangle(dwm.Width / 2 - 150, dwm.Height /2 - 150, 300, 300);
        }
        catch
        {
            Log.Info("NO WINDOW ALO");
        }

        VDOUsername = UsernameProvider.Username;
        VDOPassword = Convert.ToBase64String(Encoding.UTF8.GetBytes("UsernameProvider.Username" + "eyesquad"));
        VDOLink = $"https://vdo.ninja/?scene&room={VDOUsername}&password={VDOPassword}";
    }

    protected override async Task HandleAfterFirstRender()
    {
        Disposable.Create(() => Log.Info("Disposed")).AddTo(Anchors);

        Minimap.ImageSink
            .Where(x => x != null && Enabled)
            .Subscribe(x => Task.Run(() =>StartGetVal(x)))
            .AddTo(Anchors);

        PlayerHp.ImageSink
            .Where(x => x != null && Enabled)
            .Subscribe(x => Task.Run(() =>HpBar(x)))
            .AddTo(Anchors);

        TargetHp.ResultStream.Where(_ => Enabled).Subscribe(x => 
                Task.Run(() =>CheckTarget()))
            .AddTo(Anchors);
        
        /*TargetHp.WhenAnyValue(x => x.IsActive)
            .Where(x => x.HasValue && x.Value && Hotkey.IsActive == true)
            .Subscribe(x => Fight())
            .AddTo(Anchors);*/
        TargetHp.WhenAnyValue(x => x.IsActive)
            .Where(x => x.HasValue && !x.Value && Hotkey.IsActive == true)
            .SelectAsync(async _ =>
            {
                await SearchTarget();
                return Unit.Default; 
            })
            .Subscribe()
            .AddTo(Anchors);
        Hotkey.WhenAnyValue(x => x.IsActive)
            .Where(x => x.HasValue && x.Value)
            .Subscribe(x => Task.Run(() =>
            {
                SearchTarget();

            }))
            .AddTo(Anchors);
        Coctail.WhenAnyValue(x => x.IsActive)
            .Where(x => x.HasValue && !x.Value && EnableCoctail && Hotkey.IsActive == true)
            .Subscribe(_ => EatCoctail())
            .AddTo(Anchors);
        CharacterDead.WhenAnyValue(x => x.IsActive)
            .Where(x => x.HasValue && x.Value && Hotkey.IsActive == true)
            .Subscribe(_ => WhenImDead())
            .AddTo(Anchors);
            

        this.WhenAnyValue(x => x.Target)
            .Where(targetValue => targetValue && Hotkey.IsActive == true)
            .Subscribe(_ => Fight())
            .AddTo(Anchors);
        
        

        StartParam();
    }

    
    
    private bool Enabled { get; set; }
    private bool TogglePreview { get; set; }
    private bool Spoil { get; set; }
    private string Status { get; set; }
    private bool EnableCoctail { get; set; }
    private bool NoPickup { get; set; }
    private bool Telegram { get; set; }
    private bool GoTown { get; set; }

    private string VDOLink { get; set; } 
    private string VDOUsername { get; set; }
    private string VDOPassword { get; set; }
    
    private Point MouseClickDirect;
    private bool _target;
    private bool Target
    {
        get => _target;
        set => RaiseAndSetIfChanged(ref _target, value);
    }

    /*private async Task StartFight()
    {
        await Task.Delay(100);

        if (Target)
        {
            await Task.Delay(200);
            await Fight();

        }
        else
        {
            await SearchTarget();
        }
    }*/

    private async Task WhenImDead()
    {
        if (Telegram && TelegramAction.Token != null && TelegramAction.ChatId != null)
        {
            await Task.Run(() =>TelegramAction.Execute());
        }

        Hotkey.TriggerValue = false;
        Enabled = false;

        if (GoTown && CharacterDead.BoundsWindow.HasValue)
        {
            var rect = CharacterDead.BoundsWindow.Value;
            var point = new Point((int)(rect.X + rect.Width / 2), (int)(rect.Y + rect.Height / 2));
            await SendKey("MouseLeft", point);
        }
    }

    
    private async Task Fight()
    {
        while (Hotkey.IsActive == true && !Target)
        {
            await Task.Delay(50);
        }
        var spoiled = false;
        Status = Spoil ? "Start spoil" : "Start fight with mobs";
        if (Spoil)
        {
            while (Hotkey.IsActive == true && Spoiled.IsActive == false && Target)
            {
                await SendKey("2");
                await Task.Delay(350);
            }

            spoiled = true;
        }

        Status = "Start fight with mobs";
        Attack.TriggerValue = true;
        try
        {
            while (Target && Hotkey.IsActive == true)
            {
                await Task.Delay(100);
            }
        }
        finally
        {
            Attack.TriggerValue = false;
        }

        for (int i = 0; i < 5; i++)
        {
            if (spoiled)
            {
                await SendKey("5");
            }

            if (!NoPickup)
            {
                await SendKey("4");
                await Task.Delay(100);
            }
        }

        if (!Spoil)
        {
            await SendKey("Esc");
        }

        await Task.Delay(100);
        Log.Info("Stop attack");
    }
    
    
     
    private const int SpoilDelay = 2500;
    private const int NonSpoilDelay = 2000;
    private async Task SearchTarget()
    {
        
        await Task.Delay(Spoil ? SpoilDelay : NonSpoilDelay);
        

        Log.Info("Start search");
        
        Status = "Searching target";
        
        
        
        var sw = new Stopwatch();
        sw.Start();
        
        bool mouseClick = false;
        while (!Target && Hotkey.IsActive == true)
        {
            await SendKey("3");
            if (sw.ElapsedMilliseconds > 500 && Minimap.IsActive == true && Enabled && !mouseClick)
            {
                Task.Run(() =>MouseClickToFollow());
                mouseClick = true;
            }
            await Task.Delay(200);

        }
        sw.Stop();
        Status = "Searching exit";
    }

    private async Task MouseClickToFollow()
    {
        while (!Target  && Hotkey.IsActive == true)
        {
            
            if (Minimap.IsActive == true && Enabled)
            {
                await SendKey("MouseLeft",MouseClickDirect);
            }
            await Task.Delay(20);
        }
    }
    
    
    //
    //  НЕ РАЗЪЕБЫВАЕТ
    //  НЕ РАЗЪЕБЫВАЕТ
    //  НЕ РАЗЪЕБЫВАЕТ
    //  НЕ РАЗЪЕБЫВАЕТ
    
    
    
    private async Task CheckTarget()
    {
        bool checkBlack = await CheckBlackList();
        Target = TargetHp.IsActive == true && !checkBlack;
        
    }

    private async Task<bool> CheckBlackList()
    {
        if (IgnorTarget?.TextEvaluator?.Text == null || BlackList == null)
        {
            return false;
        }
        await IgnorTarget.FetchNextResult();
        var text = IgnorTarget.TextEvaluator.Text.ToLower();
        return BlackList.Any(item => text.Contains(item.ToLower()));
    }
    
    
    
    private async Task EatCoctail()
    {
        await SendKey("8");
        await SendKey("8");
    }
    private async Task StartGetVal(Image<Bgr, byte> inputImage)
    {
        await Task.Run(() => GetVal(inputImage));
        //Log.Info($"Full render - {sw.ElapsedMilliseconds}ms");
    }

    private double _globalPercentage = 0;

    private async Task HpBar(Image<Bgr, byte> inputImage)
    {
        if (inputImage == null)
        {
            return;
        }
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
            _globalPercentage = (double)widthOfWhitePixels / 150 * 100;

            // Ограничиваем значение до 100%
            _globalPercentage = Math.Min(_globalPercentage, 100);
            await JSRuntime.InvokeVoidAsync("updateProgressBar", _globalPercentage);
        }
        else
        {
            _globalPercentage = 0;
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

    private int _ignoreDistance = 15; // Глобальная переменная для задания расстояния игнорирования
    private Dictionary<Rectangle, DateTime> _ignoredAreas = new Dictionary<Rectangle, DateTime>();

    private Point FindClosestWhitePoint(VectorOfVectorOfPoint contours, Point centerRelativeToImage)
    {
        Point closestPoint = new Point();
        double minDistance = double.MaxValue;

        foreach (var contour in contours.ToArrayOfArray())
        {
            foreach (var pt in contour)
            {
                /*if (IsPointIgnored(pt))
                {
                    continue;
                }*/

                double distance = CalculateDistance(pt, centerRelativeToImage);
                if (distance >= _ignoreDistance && distance < minDistance)
                {
                    minDistance = distance;
                    closestPoint = pt;
                }
            }
        }

        // Добавление логики для игнорирования точек
        // Пример, нужно модифицировать в соответствии с вашей логикой
        /*if (TargetHp.IsActive == true && !Target)
        {
            Rectangle ignoreArea = new Rectangle(closestPoint.X - 15, closestPoint.Y - 15, 30, 30);
            _ignoredAreas[ignoreArea] = DateTime.Now.AddSeconds(20); // Игнорировать на 20 секунд
        }*/

        return closestPoint;
    }
    /*private bool IsPointIgnored(Point pt)
    {
        return _ignoredAreas.Any(kv => kv.Key.Contains(pt) && DateTime.Now < kv.Value);
    }*/
    private double CalculateDistance(Point p1, Point p2)
    {
        double dx = p1.X - p2.X;
        double dy = p1.Y - p2.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }


    private int _rad = 250;

    private void ProcessDirectionAndClick(Rectangle windowBounds, Point centerRelativeToImage, Point closestPoint)
    {
        double directionX = closestPoint.X - centerRelativeToImage.X;
        double directionY = closestPoint.Y - centerRelativeToImage.Y;


        double magnitude = CalculateDistance(new Point(0, 0), new Point((int)directionX, (int)directionY));
        directionX /= magnitude;
        directionY /= magnitude;


        var characterCenter = new Point(windowBounds.Width / 2, (int)(windowBounds.Height * 0.53));
        

        int clickX = characterCenter.X + (int)(directionX * _rad);
        int clickY = characterCenter.Y + (int)(directionY * _rad);

        
        if (clickX >= 0 && clickY >= 0)
        {
            MouseClickDirect = new Point(clickX, clickY);
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
        
        enlargedImage.Draw(
            new CircleF(new PointF(centerCordsRelativeToInputImage.X, centerCordsRelativeToInputImage.Y), 2),
            new Bgr(0, 255, 0), 2);
        if (closestPoint != default)
        {
            enlargedImage.Draw(new CircleF(closestPoint, 2), new Bgr(0, 0, 255), 2);
            

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
        }

        /*foreach (var area in _ignoredAreas.Keys)
        {
            if (DateTime.Now < _ignoredAreas[area])
            {
                Rectangle scaledArea = new Rectangle(
                    (area.X - centerCordsRelativeToInputImage.X / size) * size + centerCordsRelativeToInputImage.X,
                    (area.Y - centerCordsRelativeToInputImage.Y / size) * size + centerCordsRelativeToInputImage.Y,
                    area.Width * size,
                    area.Height * size
                );

                CvInvoke.Rectangle(enlargedImage, scaledArea, new Bgr(Color.Yellow).MCvScalar, -1); // Закрашивание зоны
            }
        }*/
        using (MemoryStream memory = new MemoryStream())
        {
            enlargedImage.ToBitmap().Save(memory, System.Drawing.Imaging.ImageFormat.Jpeg);
            byte[] imageBytes = memory.ToArray();
            var base64ImageString = "data:image/jpeg;base64," + Convert.ToBase64String(imageBytes);

            JSRuntime.InvokeVoidAsync("updateImage", base64ImageString);
        }
        
        
    }
    
    private async Task SendKey(string key, Point? point = null)
    {
        try
        {
            var activeWindow = TargetHp.ActiveWindow;
            if (activeWindow == null)
            {
                Log.Info("Window is null, break Send Input");
                return;
            }

            var hotkey = HotkeyConverter.ConvertFromString(key);

            if (point == null)
            {
                await SendInputController.Send(DefaultSendInputArgs with
                {
                    Window = activeWindow,
                    Gesture = hotkey,
                }, CancellationToken.None);
            }
            else
            {
                await SendInputController.Send(DefaultSendInputArgs with
                {
                    MouseLocation = point.Value,
                    Window = activeWindow,
                    Gesture = hotkey,
                }, CancellationToken.None);
            }
        }
        catch
        {
            Log.Info("Problem with input");
        }
    }

    
}