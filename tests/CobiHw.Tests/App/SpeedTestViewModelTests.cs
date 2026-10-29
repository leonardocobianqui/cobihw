using CobiHw.App;
using CobiHw.Core.Network;
using CobiHw.Tests.Fakes;

namespace CobiHw.Tests.App;

/// <summary>Speed test as the widget, the settings window and the tray show it.</summary>
/// <remarks>Tests that run the test do it on <see cref="UiThread"/>, the order the app's UI thread gives.</remarks>
[Collection(AppState.Name)]
public sealed class SpeedTestViewModelTests : LocalizedTest
{
    private static readonly SpeedTestResult Measured = new(new DateTimeOffset(2026, 10, 13, 17, 30, 0, TimeSpan.FromHours(-3)), 250e6, 48e6, 12.4, 2.1);
    private static readonly SpeedTestException RefusedDownload = new(SpeedTestFailure.Refused, SpeedTestStage.Download, 50_000_000, "403 Forbidden");

    private static SpeedTestViewModel With(HttpMessageHandler server, SpeedTestResult? lastResult = null) =>
        new(FakeCloudflare.Client(server), lastResult);

    private static async Task<string?> FailureOf(SpeedTestViewModel speedTest)
    {
        string? message = null;
        speedTest.Failed += (_, text) => message = text;
        await UiThread.RunAsync(() => speedTest.RunCommand.ExecuteAsync(null));
        return message;
    }

    [Fact]
    public async Task Refused_LargeDownload_SaysWhereItFailed()
    {
        SpeedTestViewModel speedTest = With(new RefusingCloudflare(download: true, 50_000_000));

        Assert.Equal("O teste falhou no download de 50 MB: o servidor recusou o pedido (403 Forbidden).", await FailureOf(speedTest));
        Assert.False(speedTest.IsTesting);
        Assert.True(speedTest.IsIdle);

        // The window keeps the ping that finished and shows the error; the needle goes back to zero.
        Assert.Equal(new[] { SpeedTestPhaseState.Done, SpeedTestPhaseState.Pending, SpeedTestPhaseState.Pending }, speedTest.Phases.Select(phase => phase.State));
        Assert.EndsWith(" ms", speedTest.Phases[0].ValueText, StringComparison.Ordinal);
        Assert.Equal(DisplayFormat.Unavailable, speedTest.Phases[1].ValueText);
        Assert.Equal(speedTest.StatusText, speedTest.WindowStatusText);
        Assert.Equal(0, speedTest.GaugeMegabits);
        Assert.Equal(DisplayFormat.Unavailable, speedTest.GaugeValueText);
    }

    [Fact]
    public async Task Refused_Upload_SaysWhereItFailed() =>
        Assert.Equal("O teste falhou no upload de 50 MB: o servidor recusou o pedido (403 Forbidden).", await FailureOf(With(new RefusingCloudflare(download: false, 0))));

    [Fact]
    public async Task NetworkError_BecomesAMessage_WithoutCrashing()
    {
        SpeedTestViewModel speedTest = With(new FailingHandler());

        Assert.Equal("O teste falhou na medição de latência: não foi possível falar com o servidor (No connection could be made).", await FailureOf(speedTest));
        Assert.False(speedTest.HasResult);
    }

    [Fact]
    public async Task Success_RaisesCompleted_AndFillsTheTexts()
    {
        SpeedTestViewModel speedTest = With(new FakeCloudflare());
        SpeedTestResult? completed = null;
        speedTest.Completed += (_, result) => completed = result;

        await UiThread.RunAsync(() => speedTest.RunCommand.ExecuteAsync(null));

        Assert.NotNull(completed);
        Assert.True(speedTest.HasResult);
        Assert.StartsWith("↓ ", speedTest.ResultText, StringComparison.Ordinal);
        Assert.StartsWith("Latência ", speedTest.LatencyText, StringComparison.Ordinal);
        Assert.EndsWith("· Cloudflare", speedTest.MeasuredAtText, StringComparison.Ordinal);
        Assert.StartsWith("Último teste", speedTest.StatusText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Phases_FillInOrder_AndTheGaugeFollowsThePhase()
    {
        SpeedTestViewModel speedTest = With(new FakeCloudflare());
        string? pingUnit = null;
        SpeedTestPhaseState[]? statesAtUpload = null;
        string? downloadAtUpload = null;
        bool uploadingAtUpload = false;
        string? uploadUnit = null;
        speedTest.Phases[0].PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SpeedTestPhase.State) && speedTest.Phases[0].State == SpeedTestPhaseState.Active)
            {
                pingUnit ??= speedTest.GaugeUnitText;
            }
        };
        speedTest.Phases[2].PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SpeedTestPhase.State) && speedTest.Phases[2].State == SpeedTestPhaseState.Active && statesAtUpload is null)
            {
                statesAtUpload = [.. speedTest.Phases.Select(phase => phase.State)];
                downloadAtUpload = speedTest.Phases[1].ValueText;
                uploadingAtUpload = speedTest.IsUploading;
                uploadUnit = speedTest.GaugeUnitText;
            }
        };

        await UiThread.RunAsync(() => speedTest.RunCommand.ExecuteAsync(null));
        Assert.NotNull(speedTest.LastResult);
        SpeedTestResult result = speedTest.LastResult;

        // While the ping runs, the gauge shows milliseconds.
        Assert.Equal("ms · Ping", pingUnit);

        // When the upload starts, ping and download are done with their final results, and the gauge turns to the upload.
        Assert.Equal(new[] { SpeedTestPhaseState.Done, SpeedTestPhaseState.Done, SpeedTestPhaseState.Active }, statesAtUpload);
        Assert.Equal(DisplayFormat.Bandwidth(result.DownloadBitsPerSecond), downloadAtUpload);
        Assert.True(uploadingAtUpload);
        Assert.Equal("Mbps · Upload", uploadUnit);

        // At the end every card has its result and the needle rests on the download.
        Assert.All(speedTest.Phases, phase => Assert.Equal(SpeedTestPhaseState.Done, phase.State));
        Assert.Equal(DisplayFormat.Milliseconds(result.LatencyMilliseconds), speedTest.Phases[0].ValueText);
        Assert.Equal($"jitter {DisplayFormat.Milliseconds(result.JitterMilliseconds)}", speedTest.Phases[0].DetailText);
        Assert.Equal(DisplayFormat.Bandwidth(result.UploadBitsPerSecond), speedTest.Phases[2].ValueText);
        Assert.Equal(string.Empty, speedTest.Phases[2].DetailText);
        Assert.Equal(result.DownloadBitsPerSecond / 1_000_000, speedTest.GaugeMegabits);
        Assert.Equal(DisplayFormat.BandwidthNumber(result.DownloadBitsPerSecond), speedTest.GaugeValueText);
        Assert.False(speedTest.IsUploading);
        Assert.Equal(speedTest.MeasuredAtText, speedTest.WindowStatusText);
    }

    [Fact]
    public async Task Cancel_KeepsTheLastResult()
    {
        SpeedTestViewModel speedTest = With(new FakeCloudflare(), Measured);

        await UiThread.RunAsync(async () =>
        {
            Task running = speedTest.RunCommand.ExecuteAsync(null);
            await Task.Delay(300, TestContext.Current.CancellationToken);

            Assert.True(speedTest.IsTesting);
            Assert.False(speedTest.IsIdle);
            Assert.NotEmpty(speedTest.LiveText);

            speedTest.RunCancelCommand.Execute(null);
            await running;
        });

        Assert.Equal("Teste cancelado.", speedTest.StatusText);
        Assert.Equal(Measured, speedTest.LastResult);
        Assert.False(speedTest.IsTesting);
        // The window says it was cancelled, no phase stays active and the needle goes back to zero.
        Assert.Equal("Teste cancelado.", speedTest.WindowStatusText);
        Assert.DoesNotContain(speedTest.Phases, phase => phase.State == SpeedTestPhaseState.Active);
        Assert.Equal(0, speedTest.GaugeMegabits);
    }

    [Fact]
    public void LastResult_Texts()
    {
        SpeedTestViewModel speedTest = With(new FakeCloudflare(), Measured);

        Assert.Equal("↓ 250 Mbps · ↑ 48,0 Mbps", speedTest.ResultText);
        Assert.Equal("Latência 12 ms · jitter 2 ms", speedTest.LatencyText);
    }

    [Fact]
    public void Texts_FollowTheLanguage()
    {
        SpeedTestViewModel speedTest = With(new FakeCloudflare(), Measured);

        SetLanguage("en-US");
        Assert.Equal("↓ 250 Mbps · ↑ 48.0 Mbps", speedTest.ResultText);
        Assert.Equal("Latency 12 ms · jitter 2 ms", speedTest.LatencyText);
        Assert.StartsWith("Last test (", speedTest.StatusText, StringComparison.Ordinal);
        Assert.Equal("The test failed during the 50 MB download: the server refused the request (403 Forbidden).", SpeedTestViewModel.FailureText(RefusedDownload));

        SetLanguage("es-ES");
        Assert.Equal("La prueba falló en la descarga de 50 MB: el servidor rechazó la solicitud (403 Forbidden).", SpeedTestViewModel.FailureText(RefusedDownload));

        SetLanguage("ru-RU");
        Assert.Equal("Тест не удался: ни одно измерение не было корректным.", SpeedTestViewModel.FailureText(new SpeedTestException(SpeedTestFailure.NoValidMeasurement)));
        Assert.Equal("Мбит/с · Загрузка", speedTest.GaugeUnitText);
        Assert.Equal(new[] { "Пинг", "Загрузка", "Отдача" }, speedTest.Phases.Select(phase => phase.Name));
    }
}
