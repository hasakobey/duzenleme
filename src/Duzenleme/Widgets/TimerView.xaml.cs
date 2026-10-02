using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Duzenleme.Core;

namespace Duzenleme.Widgets;

/// <summary>
/// Zamanlayıcı, Pomodoro ve kronometre (<see cref="WidgetVariants.Timer"/>; temel türü Clock: 2.0 onu saat olarak gösterir).
/// Durum UTC'dir (<see cref="TimerState"/>, hesap <see cref="TimerLogic"/>) ve yalnızca başlat, duraklat, sıfırla ve aşama
/// sonunda kaydedilir. Görünürken ve çalışırken saniyede bir güncellenir; görünmezken yalnızca bitiş anına kurulu tek bir
/// zamanlayıcı kalır (süre dolunca bildirim yine gelir). Uygulama kapalıyken dolan süre açılışta "Süre doldu" olarak görünür,
/// sonradan alarm çalmaz.
/// </summary>
public partial class TimerView : UserControl, IWidgetView
{
    private static CultureInfo Culture => L.Culture;

    private readonly WidgetConfig _config;
    private readonly DispatcherTimer _alarm;
    private readonly WidgetNameLine _name;
    private WidgetPalette _palette = WidgetPalette.Glass;
    private bool _live, _ticking, _alert;

    public TimerView(WidgetConfig config)
    {
        _config = config;
        _config.Timer ??= new TimerState();
        InitializeComponent();
        _name = new WidgetNameLine(this, config, NameBox, NameText, NameShadow, Render);
        _alarm = new DispatcherTimer(DispatcherPriority.Normal);
        _alarm.Tick += (_, _) => CheckEnd(whileClosed: false);
        AutomationProperties.SetName(ResetButton, L.T("Sıfırla"));
        AutomationProperties.SetName(PlusButton, L.T("Bir dakika ekle"));
        ResetButton.ToolTip = L.T("Sıfırla (R)");
        PlusButton.ToolTip = L.T("Bir dakika ekle");
        PreviewMouseLeftButtonDown += (_, _) =>
        {
            if (!_name.IsEditing) Focus();
            ClearAlert();
        };
        PreviewKeyDown += OnKey;
        // Uygulama kapalıyken dolan süre: alarm yok, "Süre doldu" görünür.
        CheckEnd(whileClosed: true);
        Arm();
        Render();
    }

    private TimerState State => _config.Timer!;

    private string Mode => TimerLogic.Mode(State);

    public bool Resizable => false;

    protected override Geometry? GetLayoutClip(Size layoutSlotSize) => ClipToBounds ? base.GetLayoutClip(layoutSlotSize) : null;

    public void SetLive(bool live)
    {
        if (_live == live) return;
        _live = live;
        if (live) CheckEnd(whileClosed: false); // uykudan uyanınca bitmiş olabilir
        UpdateTicking();
        if (live) Render();
    }

    /// <summary>Saniye bildirimi yalnızca görünürken ve çalışırken.</summary>
    private void UpdateTicking()
    {
        var want = _live && TimerLogic.IsRunning(State);
        if (want == _ticking) return;
        _ticking = want;
        if (want) WidgetTicker.SecondTick += OnSecond;
        else WidgetTicker.SecondTick -= OnSecond;
    }

    private void OnSecond()
    {
        // Bitiş anı saniye sınırına denk gelmeyebilir: tik bitişi geçtiyse aşama burada da tamamlanır.
        if (!CheckEnd(whileClosed: false)) Render();
    }

    /// <summary>Bitiş anına tek atımlık zamanlayıcı (görünmezken de çalar); çalışmıyorsa durur.</summary>
    private void Arm()
    {
        _alarm.Stop();
        if (State.EndsUtc is not { } ends) return;
        var wait = ends - DateTime.UtcNow;
        _alarm.Interval = wait > TimeSpan.Zero ? wait + TimeSpan.FromMilliseconds(20) : TimeSpan.FromMilliseconds(1);
        _alarm.Start();
    }

    /// <summary>Süre dolduysa aşamayı bitirir, kaydeder, (açıkken) bildirir. Bir şey bittiyse true.</summary>
    private bool CheckEnd(bool whileClosed)
    {
        var end = TimerLogic.Complete(State, DateTime.UtcNow, whileClosed);
        if (end is null) return false;
        AppHost.SaveSettings();
        Arm();
        UpdateTicking();
        if (!whileClosed) Alarm(end);
        Render();
        return true;
    }

    /// <summary>
    /// Süre doldu: tepside bildirim (ayar açıksa; Windows'un odak/rahatsız etme ayarlarına uyar), widget birkaç saniye öne
    /// gelir (widget'lar gizli değilse; Canlı'da bir kez nabız atarak) ve kartta durağan vurgu çerçevesi kalır.
    /// </summary>
    private void Alarm(TimerEnd end)
    {
        _alert = true;
        if (State.Notify)
        {
            var (title, text) = end.Mode == TimerModes.Pomodoro
                ? end.FinishedPhase == TimerModes.Focus
                    ? (L.T("Odak bitti"), end.NextPhase == TimerModes.LongBreak ? L.T("Uzun mola zamanı.") : L.T("Kısa bir mola zamanı."))
                    : (L.T("Mola bitti"), L.T("Yeniden odaklanma zamanı."))
                : (L.T("Süre doldu"), L.F("{0} · {1}", WidgetText.DisplayName(_config), end.EndedUtc.ToLocalTime().ToString("t", Culture)));
            // Adlı Pomodoro'da hangisinin olduğu da söylenir ("Çalışma · Kısa bir mola zamanı.").
            if (end.Mode == TimerModes.Pomodoro && _name.Name is { } name) text = L.F("{0} · {1}", name, text);
            var id = _config.Id;
            AppHost.Tray?.Notify(title, text, () => AppHost.Widgets.Reveal(id));
        }
        if (Window.GetWindow(this) is WidgetWindow { IsVisible: true } window)
        {
            window.Reveal(TimeSpan.FromSeconds(5));
            // Kart bir kez nabız atar (yanıp sönme yok; Sakin ve Kapalı'da yalnızca çerçeve).
            window.Attention();
        }
    }

    private void ClearAlert()
    {
        if (!_alert) return;
        _alert = false;
        Render();
    }

    private void Render()
    {
        var now = DateTime.UtcNow;
        var mode = Mode;
        var running = TimerLogic.IsRunning(State);
        DigitsText.Text = mode == TimerModes.Stopwatch
            ? TimerLogic.Format(TimerLogic.Elapsed(State, now), roundUp: false)
            : TimerLogic.Format(TimerLogic.Remaining(State, now), roundUp: true);
        PhaseText.Text = PhaseLine(now);

        var progress = mode == TimerModes.Stopwatch ? 0 : TimerLogic.Progress(State, now);
        DoneColumn.Width = new GridLength(progress, GridUnitType.Star);
        LeftColumn.Width = new GridLength(1 - progress, GridUnitType.Star);

        StartButton.Content = running ? "" : "";   // Duraklat / Başlat
        var startName = running ? L.T("Duraklat") : L.T("Başlat");
        AutomationProperties.SetName(StartButton, startName);
        StartButton.ToolTip = L.F("{0} (Boşluk)", startName);
        PlusButton.Visibility = mode == TimerModes.Countdown ? Visibility.Visible : Visibility.Collapsed;
        ProgressBox.Visibility = mode != TimerModes.Stopwatch && _config.Shows("progress") ? Visibility.Visible : Visibility.Collapsed;
        PhaseBox.Visibility = _config.Shows("phase") ? Visibility.Visible : Visibility.Collapsed;
        ButtonRow.Visibility = _config.Shows("buttons") ? Visibility.Visible : Visibility.Collapsed;
        Corner.Update(_config);
        AlertFrame.BorderBrush = _alert ? _palette.Accent : Brushes.Transparent;
        _name.Render();
        AutomationProperties.SetName(this, string.Join(", ", new[] { _name.Name, PhaseText.Text, DigitsText.Text }.OfType<string>()));
    }

    /// <summary>"Zamanlayıcı · 10 dk", "Odak · 2/4", "Süre doldu · 14:32", "Kronometre · duraklatıldı".</summary>
    private string PhaseLine(DateTime now)
    {
        var mode = Mode;
        var running = TimerLogic.IsRunning(State);
        if (State.FinishedUtc is { } finished && !running)
        {
            var at = finished.ToLocalTime().ToString("t", Culture);
            return mode == TimerModes.Pomodoro
                ? L.F("Süre doldu · {0} · Sırada: {1}", at, PhaseName(State.Phase))
                : L.F("Süre doldu · {0}", at);
        }
        var paused = TimerLogic.IsPaused(State) ? " · " + L.T("duraklatıldı") : "";
        return mode switch
        {
            TimerModes.Pomodoro => TimerModes.NormalizePhase(State.Phase) == TimerModes.Focus
                ? L.F("Odak · {0}/{1}", State.Round, Math.Max(1, State.RoundsBeforeLong)) + paused
                : PhaseName(State.Phase) + paused,
            TimerModes.Stopwatch => L.T("Kronometre") + paused,
            _ => L.F("Zamanlayıcı · {0} dk", Math.Clamp(State.Minutes, TimerLogic.MinMinutes, TimerLogic.MaxMinutes)) + paused,
        };
    }

    private static string PhaseName(string phase) => TimerModes.NormalizePhase(phase) switch
    {
        TimerModes.Break => L.T("Mola"),
        TimerModes.LongBreak => L.T("Uzun mola"),
        _ => L.T("Odak"),
    };

    /// <summary>Durum değişti (başlat, duraklat, sıfırla, kip, süre): kaydet, alarmı ve saniye bildirimini yeniden kur, çiz.</summary>
    private void Changed()
    {
        _alert = false;
        AppHost.SaveSettings();
        Arm();
        UpdateTicking();
        Render();
    }

    private void StartPause()
    {
        if (TimerLogic.IsRunning(State)) TimerLogic.Pause(State, DateTime.UtcNow);
        else TimerLogic.Start(State, DateTime.UtcNow);
        Changed();
    }

    private void Reset()
    {
        TimerLogic.Reset(State);
        Changed();
    }

    private void AddMinute()
    {
        TimerLogic.AddMinute(State, DateTime.UtcNow);
        Changed();
    }

    private void Start_Click(object sender, RoutedEventArgs e) => StartPause();

    private void Reset_Click(object sender, RoutedEventArgs e) => Reset();

    private void Plus_Click(object sender, RoutedEventArgs e) => AddMinute();

    private void OnKey(object sender, KeyEventArgs e)
    {
        if (_name.IsEditing) return; // boşluk ve R ada yazılır
        switch (e.Key)
        {
            case Key.Space: StartPause(); break;
            case Key.R: Reset(); break;
            default: return;
        }
        e.Handled = true;
    }

    public void ApplyPalette(WidgetPalette palette)
    {
        _palette = palette;
        _name.ApplyPalette(palette);
        PhaseText.Foreground = palette.Secondary;
        Fill.Background = palette.Accent;
        Track.Background = palette.BorderBrush;
        foreach (var button in new[] { StartButton, ResetButton, PlusButton }) button.Foreground = palette.Foreground;
        Corner.Paint(palette);
        foreach (var shadow in new[] { PhaseShadow, DigitsShadow }) shadow.Show(palette.TextShadow);
        ClearTypeText.Follow(this, PhaseText, PlusButton);
        Render();
    }

    /// <summary>Zamanlayıcının hazır süreleri (Düzenle panelinde yongalar; başkası "Başka süre…" ile).</summary>
    private static readonly int[] Presets = [1, 5, 10, 15, 25, 30, 45, 60];

    /// <summary>Menüde: Başlat/Duraklat ve Sıfırla (kartın kısayollarıyla). Ayarlar Düzenle panelinde.</summary>
    public void AddMenuItems(WidgetMenu menu)
    {
        var running = TimerLogic.IsRunning(State);
        menu.Primary.Add(Menus.Item(running ? L.T("Duraklat") : L.T("Başlat"), StartPause, L.T("Boşluk")));
        menu.Primary.Add(Menus.Item(L.T("Sıfırla"), Reset, "R"));
        if (!menu.ForEditor) return;
        if (Mode == TimerModes.Pomodoro)
            menu.More.Add(Menus.Toggle(L.T("Sonraki aşama kendiliğinden başlasın"), () => State.AutoStartNext, () =>
            {
                State.AutoStartNext = !State.AutoStartNext;
                AppHost.SaveSettings();
            }));
        menu.Settings.Add(Menus.Parts(_config,
            [WidgetNameLine.Part, ("phase", L.N("Durum satırı")), ("progress", L.N("İlerleme çubuğu")), ("buttons", L.N("Düğmeler")),
             Menus.EditPart, Menus.ClosePart],
            Render));
    }

    /// <summary>
    /// Düzenle paneli: "Tür" (Zamanlayıcı · Pomodoro · Kronometre; panel yeniden kurulur), zamanlayıcının süresi (yongalar ve
    /// "Başka süre…"), Pomodoro'nun süreleri (25 / 5 / 15 · 50 / 10 / 30 · "Özel…") ve "Süre dolunca bildir". Çalışan aşamanın
    /// süresi değişmez; durmuşsa yeni süre hemen görünür.
    /// </summary>
    public bool AddEditorContent(Views.EditorContent content)
    {
        content.Segments(L.T("Tür"),
            [
                new(TimerModes.Countdown, L.T("Zamanlayıcı"), "Editor.Timer.Countdown"),
                new(TimerModes.Pomodoro, L.T("Pomodoro"), "Editor.Timer.Pomodoro"),
                new(TimerModes.Stopwatch, L.T("Kronometre"), "Editor.Timer.Stopwatch"),
            ],
            () => Mode, mode =>
            {
                if (mode == Mode) return;
                TimerLogic.SetMode(State, mode);
                Changed();
            }, rebuild: true);
        switch (Mode)
        {
            case TimerModes.Countdown:
                var minutes = Presets.Select(m => new Views.EditorChoice(Id(m), L.F("{0} dk", m), "Editor.Timer.Minutes." + Id(m))).ToList();
                // Kayıtlı başka bir süre seçili yonga olarak görünür (kaybolmaz).
                if (!Presets.Contains(State.Minutes))
                    minutes.Add(new(Id(State.Minutes), L.F("{0} dk", State.Minutes), "Editor.Timer.Minutes.Custom"));
                minutes.Add(new("other", L.T("Başka süre…"), "Editor.Timer.Minutes.Other", OpensWindow: true));
                content.Chips(L.T("Süre"), minutes, () => Id(State.Minutes), id =>
                {
                    if (id == "other") AskMinutes();
                    else if (int.TryParse(id, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value != State.Minutes)
                        SetMinutes(value);
                }, rebuild: true);
                break;
            case TimerModes.Pomodoro:
                var presets = TimerLogic.PomodoroPresets;
                var choices = presets.Select((p, i) => new Views.EditorChoice(Id(i), L.F("{0} / {1} / {2} dk", p.Focus, p.Break, p.LongBreak),
                    "Editor.Timer.Pomodoro." + (i + 1))).ToList();
                choices.Add(new("custom", L.T("Özel…"), "Editor.Timer.Pomodoro.Custom", OpensWindow: true));
                content.Chips(L.T("Süre"), choices, () =>
                {
                    var index = presets.ToList().IndexOf(TimerLogic.PomodoroMinutes(State));
                    return index < 0 ? "custom" : Id(index);
                }, id =>
                {
                    if (id == "custom") AskPomodoro();
                    else if (int.TryParse(id, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index) && index < presets.Count)
                    {
                        TimerLogic.SetPomodoro(State, presets[index]);
                        Changed();
                    }
                });
                break;
        }
        if (Mode != TimerModes.Stopwatch)
            content.Switch(L.T("Süre dolunca bildir"), "Editor.Timer.Notify", () => State.Notify, on =>
            {
                State.Notify = on;
                AppHost.SaveSettings();
            });
        return true;
    }

    private static string Id(int value) => value.ToString(CultureInfo.InvariantCulture);

    /// <summary>Zamanlayıcının süresi; çalışmıyorsa yeni süre hemen görünür (çalışan aşamanın süresi değişmez).</summary>
    private void SetMinutes(int minutes)
    {
        TimerLogic.SetMinutes(State, minutes);
        if (!TimerLogic.IsRunning(State)) State.RemainingTicks = null;
        Changed();
    }

    /// <summary>F2: zamanlayıcının adı (bkz. <see cref="WidgetNameLine"/>; Düzenle panelinde ad kutusu).</summary>
    public bool TryBeginRename() => _name.Begin();

    private void AskPomodoro()
    {
        var (focus, rest, longRest) = TimerLogic.PomodoroMinutes(State);
        var label = L.T("Odak / mola / uzun mola (dakika)");
        var text = $"{focus} / {rest} / {longRest}";
        // Okunamayan yanıt sessizce yok sayılmaz: pencere ne yazılacağını söyleyerek yazılanla yeniden açılır (Vazgeç bırakır).
        while (InputDialog.Ask(L.T("Pomodoro"), label, text, (Window.GetWindow(this) as WidgetWindow)?.CenterPoint) is { } answer)
        {
            if (TimerLogic.TryParsePomodoro(answer, out var minutes))
            {
                TimerLogic.SetPomodoro(State, minutes);
                Changed();
                return;
            }
            text = answer;
            label = L.T("Üç süreyi dakika olarak yaz, ör. 25 / 5 / 15.");
        }
    }

    private void AskMinutes()
    {
        if (InputDialog.Ask(L.T("Zamanlayıcı"), L.T("Süre (dakika)"), State.Minutes.ToString(Culture),
                (Window.GetWindow(this) as WidgetWindow)?.CenterPoint) is not { } text) return;
        if (!int.TryParse(text.Trim(), NumberStyles.Integer, Culture, out var minutes) || minutes < TimerLogic.MinMinutes) return;
        SetMinutes(minutes);
    }

    public void Detach()
    {
        _name.Cancel();
        _alarm.Stop();
        if (_ticking) WidgetTicker.SecondTick -= OnSecond;
        _ticking = false;
    }
}
