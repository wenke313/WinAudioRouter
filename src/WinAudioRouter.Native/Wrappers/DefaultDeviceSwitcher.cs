using System.Runtime.InteropServices;
using WinAudioRouter.Native.Interop;

namespace WinAudioRouter.Native.Wrappers;

public static class DefaultDeviceSwitcher
{
    private const int S_OK = 0;
    private const int RoleMultimedia = 0;
    private const int RoleCommunications = 2;

    public static SwitchResult SwitchDefaultDevice(string deviceId, AudioDataFlow dataFlow)
    {
        if (string.IsNullOrEmpty(deviceId))
            return new SwitchResult { Success = false, Error = "Device ID is empty" };

        IPolicyConfigVista? policyConfig = null;
        try
        {
            policyConfig = (IPolicyConfigVista)new PolicyConfigClient();

            int hr1 = policyConfig.SetDefaultEndpoint(deviceId, RoleMultimedia);
            int hr2 = policyConfig.SetDefaultEndpoint(deviceId, RoleCommunications);

            if (hr1 != S_OK || hr2 != S_OK)
            {
                return new SwitchResult
                {
                    Success = false,
                    Error = $"SetDefaultEndpoint failed: multimedia=0x{hr1:X8}, communications=0x{hr2:X8}"
                };
            }

            return new SwitchResult { Success = true };
        }
        catch (COMException ex)
        {
            return new SwitchResult
            {
                Success = false,
                Error = $"COM error: 0x{ex.ErrorCode:X8} - {ex.Message}"
            };
        }
        catch (Exception ex)
        {
            return new SwitchResult
            {
                Success = false,
                Error = $"{ex.GetType().Name}: {ex.Message}"
            };
        }
        finally
        {
            if (policyConfig is not null && Marshal.IsComObject(policyConfig))
            {
                Marshal.FinalReleaseComObject(policyConfig);
            }
        }
    }
}

public class SwitchResult
{
    public bool Success { get; init; }
    public string? Error { get; init; }
}

public enum AudioDataFlow
{
    Render,
    Capture,
    All
}
