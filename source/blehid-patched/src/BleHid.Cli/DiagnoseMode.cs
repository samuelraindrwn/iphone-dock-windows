using System.Text;
using BleHid.Core;

namespace BleHid.Cli;

/// <summary>Produces a saved report even if startup, environment access, or cleanup fails.</summary>
internal static class DiagnoseMode
{
    public static Task<int> RunAsync(bool requireEncryption) =>
        RunReportAsync(requireEncryption, singleProbe: null);

    public static Task<int> RunSingleProbeAsync(string singleProbe) =>
        RunReportAsync(requireEncryption: true, singleProbe);

    private static async Task<int> RunReportAsync(bool requireEncryption, string? singleProbe)
    {
        var report = new StringBuilder();
        var gate = new object();
        void Emit(string line)
        {
            lock (gate)
            {
                Console.WriteLine(line);
                report.AppendLine(line);
            }
        }

        var advertising = false;
        var cleanupSucceeded = true;
        var diagnosticFailed = false;
        var startupMode = PeripheralStartupMode.None;
        Emit($"BLE HID diagnostics  {DateTime.Now:s}");
        Emit(new string('-', 60));

        try
        {
            try { Emit(await BluetoothDiagnostics.DescribeEnvironmentAsync()); }
            catch (Exception ex) { Emit($"environment probe failed: {ex.Message}"); }

            Emit(new string('-', 60));
            if (singleProbe is not null)
            {
                // Invoke each kind in its own process so earlier GATT registrations cannot
                // contaminate the next result. No complete HID peripheral is constructed here.
                Emit($"Single advertising probe: {singleProbe}");
                var result = await AdvertisingProbe.RunSingleAsync(singleProbe);
                advertising = result.Ok;
                Emit($"[{(result.Ok ? " ok " : "FAIL")}] {result.Label}: {result.Detail}");
                Emit("This checks advertisement startup only; it does not establish HID input subscriptions.");
                return advertising ? 0 : 1;
            }

            Emit("Starting peripheral...");
            BleHidPeripheral? peripheral = null;
            try
            {
                peripheral = new BleHidPeripheral(requireEncryption);
                peripheral.Log += Emit;
                await peripheral.StartAsync();
                advertising = peripheral.HasStartedSuccessfully;
                startupMode = peripheral.StartupMode;
            }
            catch (Exception ex)
            {
                Emit($"startup threw: {ex}");
            }
            finally
            {
                if (peripheral is not null)
                {
                    try { Emit($"Advertisement status: {peripheral.AdvertisementStatus}"); }
                    catch (Exception ex) { Emit($"Advertisement status unavailable: {ex.Message}"); }
                    try
                    {
                        await peripheral.DisposeAsync();
                        if (!peripheral.CleanupSucceeded)
                        {
                            cleanupSucceeded = false;
                            Emit("peripheral cleanup failed: one or more native cleanup operations failed; see details above.");
                        }
                    }
                    catch (Exception ex)
                    {
                        cleanupSucceeded = false;
                        Emit($"peripheral cleanup failed: {ex.Message}");
                    }
                    peripheral.Log -= Emit;
                }
            }

            Emit(new string('-', 60));
            if (!cleanupSucceeded)
            {
                Emit("Peripheral cleanup could not be confirmed. This diagnostic is incomplete; no input hooks were installed.");
            }
            else if (advertising && startupMode == PeripheralStartupMode.ExistingConnectionVerified)
            {
                Emit("Existing HID connection verified. This diagnostic has now closed the peripheral.");
                Emit("Advertising is not confirmed. Only targeted neutral reports were checked; no input hooks were installed.");
                Emit("Restart control and verify actual iPhone input. This does not establish new-device discovery.");
            }
            else if (advertising)
            {
                Emit("Advertising startup succeeded. This diagnostic has now stopped advertising.");
                Emit("Restart control to pair, and verify keyboard/mouse subscribers before redirecting input.");
            }
            else
            {
                Emit("Advertising startup failed. Keyboard and mouse capture was not started.");
                try
                {
                    var facts = await BluetoothDiagnostics.GetRadioFactsAsync();
                    if (!facts.PeripheralRole)
                        Emit("The default adapter did not report LE peripheral support (or no adapter was found).");
                    if (facts.RadiosOn > 1)
                        Emit($"{facts.RadiosOn} Bluetooth radios are on; the default adapter may differ from the intended one.");
                }
                catch (Exception ex) { Emit($"radio capabilities unavailable: {ex.Message}"); }

                Emit("\nBluetooth policy:");
                try { Emit(AdvertisingProbe.ReadPolicy().Text); }
                catch (Exception ex) { Emit($"policy lookup unavailable: {ex.Message}"); }
                Emit("\nAdvertisement budget:");
                try { Emit(AdvertisingProbe.DescribeAdvertisementBudget()); }
                catch (Exception ex) { Emit($"budget lookup unavailable: {ex.Message}"); }

                Emit("\nFor independent checks, stop other BLE HID instances, then run each command in a fresh process:");
                Emit("  BleHid.Cli.exe --probe-advertising bare");
                Emit("  BleHid.Cli.exe --probe-advertising custom");
                Emit("  BleHid.Cli.exe --probe-advertising hid");
                Emit("StopAdvertising was attempted even after failed startup. Windows does not expose a GATT-provider");
                Emit("dispose method, so this report avoids additional same-process probes that could reuse registrations.");
                Emit("A failed probe alone does not establish whether the cause is a driver, Windows state, or this implementation.");
            }
        }
        catch (Exception ex)
        {
            diagnosticFailed = true;
            Emit($"diagnostic failed: {ex}");
        }
        finally
        {
            try
            {
                var fileName = singleProbe is null ? "diagnostics.txt" : $"diagnostics-{singleProbe}.txt";
                var path = AppPaths.InLogs(fileName);
                string snapshot;
                lock (gate) snapshot = report.ToString();
                await File.WriteAllTextAsync(path, snapshot);
                Console.WriteLine($"\nSaved to {path}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nCould not save the report: {ex.Message}. The report remains in console output.");
            }
        }

        return advertising && cleanupSucceeded && !diagnosticFailed ? 0 : 1;
    }
}
