using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Management;
using Microsoft.Win32;

namespace JewskiTweaksGUI
{
    public enum RiskLevel { Safe, Moderate, Advanced }

    public class Tweak
    {
        public string Category;
        public string Name;
        public string Desc;
        public RiskLevel Risk;
        public bool Recommended;
        public Action<Action<string>> Apply;
        public Action<Action<string>> Revert;
    }

    // ===================== Shell helpers =====================
    public static class Sh
    {
        public static string RunCmd(string command)
        {
            try
            {
                var psi = new ProcessStartInfo("cmd.exe", "/c " + command)
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using (var p = Process.Start(psi))
                {
                    string o1 = p.StandardOutput.ReadToEnd();
                    string o2 = p.StandardError.ReadToEnd();
                    p.WaitForExit(60000);
                    return (o1 + o2).Trim();
                }
            }
            catch (Exception ex) { return "ERROR: " + ex.Message; }
        }

        public static string RunPS(string script)
        {
            string tmp = Path.Combine(Path.GetTempPath(), "jw_" + Guid.NewGuid().ToString("N") + ".ps1");
            try
            {
                File.WriteAllText(tmp, script);
                var psi = new ProcessStartInfo("powershell.exe",
                    "-NoProfile -ExecutionPolicy Bypass -File \"" + tmp + "\"")
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using (var p = Process.Start(psi))
                {
                    string o1 = p.StandardOutput.ReadToEnd();
                    string o2 = p.StandardError.ReadToEnd();
                    p.WaitForExit(120000);
                    return (o1 + o2).Trim();
                }
            }
            catch (Exception ex) { return "ERROR: " + ex.Message; }
            finally { try { File.Delete(tmp); } catch { } }
        }

        public static string Sc(string svc, string startType)
        {
            return RunCmd("sc config \"" + svc + "\" start= " + startType);
        }
        public static string Stop(string svc) { return RunCmd("net stop \"" + svc + "\""); }
        public static string Start(string svc) { return RunCmd("net start \"" + svc + "\""); }
        public static string Reg(string args) { return RunCmd("reg " + args); }
        public static string Task(string name, bool enable)
        {
            return RunCmd("schtasks /Change /TN \"" + name + "\" /" + (enable ? "Enable" : "Disable"));
        }
        public static string DisableSvc(string svc)
        {
            return Sc(svc, "disabled") + "\r\n" + Stop(svc);
        }
        public static string RestoreSvc(string svc, string startType)
        {
            return Sc(svc, startType) + "\r\n" + Start(svc);
        }
    }

    // ===================== Catalog =====================
    public static class Catalog
    {
        static Tweak T(string cat, string name, string desc, RiskLevel risk, bool rec,
            Action<Action<string>> apply, Action<Action<string>> revert)
        {
            return new Tweak { Category = cat, Name = name, Desc = desc, Risk = risk, Recommended = rec, Apply = apply, Revert = revert };
        }

        static Tweak Svc(string cat, string display, string svc, string desc, RiskLevel risk, bool rec, string revertStart)
        {
            return T(cat, display + "   [" + svc + "]", desc, risk, rec,
                log => log(Sh.DisableSvc(svc)),
                log => log(Sh.RestoreSvc(svc, revertStart)));
        }

        public static List<Tweak> Build()
        {
            var list = new List<Tweak>();
            Power(list);
            Network(list);
            Audio(list);
            Input(list);
            Visuals(list);
            Privacy(list);
            Gaming(list);
            Startup(list);
            Debloat(list);
            ServicesSafe(list);
            ServicesAdvanced(list);
            Tasks(list);
            Storage(list);
            RamBoot(list);
            return list;
        }

        const string CAT_POWER = "POWER && CPU";
        const string CAT_NET = "NETWORK && ETHERNET";
        const string CAT_AUDIO = "AUDIO";
        const string CAT_INPUT = "INPUT";
        const string CAT_VISUAL = "VISUAL EFFECTS && UI";
        const string CAT_PRIVACY = "PRIVACY && TELEMETRY";
        const string CAT_GAMING = "GAMING && GPU";
        const string CAT_STARTUP = "STARTUP (REGISTRY)";
        const string CAT_DEBLOAT = "DEBLOAT";
        const string CAT_SVC = "SERVICES";
        const string CAT_SVC_ADV = "SERVICES (ADVANCED)";
        const string CAT_TASKS = "SCHEDULED TASKS";
        const string CAT_STORAGE = "STORAGE && CLEANUP";
        const string CAT_RAM = "RAM && BOOT TUNING";

        static void Power(List<Tweak> l)
        {
            l.Add(T(CAT_POWER, "Ultimate Performance power plan",
                "Duplicates Windows' hidden Ultimate Performance scheme and activates it as 'Jewski Ultra Performance'. Removes CPU idle micro-latency. Slightly higher idle power draw.",
                RiskLevel.Moderate, true,
                log => log(Sh.RunPS(
                    "$out = powercfg -duplicatescheme e9a42b02-d5df-448d-aa00-03f14749eb61\r\n" +
                    "$g = ($out | Select-String '[0-9a-fA-F-]{36}').Matches[0].Value\r\n" +
                    "if ($g) { powercfg -setactive $g; powercfg -changename $g 'Jewski Ultra Performance' } else { powercfg -setactive 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c }")),
                log => log(Sh.RunCmd("powercfg -setactive 381b4222-f694-41f0-9685-ff5bb260df2e"))));

            l.Add(T(CAT_POWER, "Disable CPU core parking",
                "Keeps all CPU cores unparked at all times. Small latency win in bursty loads, slightly higher idle power.",
                RiskLevel.Safe, true,
                log => { log(Sh.RunCmd("powercfg -setacvalueindex scheme_current sub_processor CPMINCORES 100")); log(Sh.RunCmd("powercfg -setactive scheme_current")); },
                log => { log(Sh.RunCmd("powercfg -setacvalueindex scheme_current sub_processor CPMINCORES 5")); log(Sh.RunCmd("powercfg -setactive scheme_current")); }));

            l.Add(T(CAT_POWER, "CPU min/max state = 100%",
                "Disables CPU downclocking entirely (no idle power-saving). Removes clock-ramp stutter. Higher idle temps/power.",
                RiskLevel.Moderate, true,
                log => { log(Sh.RunCmd("powercfg -setacvalueindex scheme_current sub_processor PROCTHROTTLEMIN 100")); log(Sh.RunCmd("powercfg -setacvalueindex scheme_current sub_processor PROCTHROTTLEMAX 100")); log(Sh.RunCmd("powercfg -setactive scheme_current")); },
                log => { log(Sh.RunCmd("powercfg -setacvalueindex scheme_current sub_processor PROCTHROTTLEMIN 5")); log(Sh.RunCmd("powercfg -setacvalueindex scheme_current sub_processor PROCTHROTTLEMAX 100")); log(Sh.RunCmd("powercfg -setactive scheme_current")); }));

            l.Add(T(CAT_POWER, "Aggressive processor boost mode",
                "Lets Turbo/boost clocks engage instantly under load instead of ramping gradually.",
                RiskLevel.Safe, true,
                log => log(Sh.RunCmd("powercfg -setacvalueindex scheme_current sub_processor PERFBOOSTMODE 2 & powercfg -setactive scheme_current")),
                log => log(Sh.RunCmd("powercfg -setacvalueindex scheme_current sub_processor PERFBOOSTMODE 1 & powercfg -setactive scheme_current"))));

            l.Add(T(CAT_POWER, "Disable USB selective suspend",
                "Stops Windows power-cycling USB ports/devices. Prevents random mouse/controller wake-lag.",
                RiskLevel.Safe, true,
                log => log(Sh.RunCmd("powercfg -setacvalueindex scheme_current 2a737441-1930-4402-8d77-b2bebba308a3 48e6b7a6-50f5-4782-a5d4-53bb8f07e226 0 & powercfg -setactive scheme_current")),
                log => log(Sh.RunCmd("powercfg -setacvalueindex scheme_current 2a737441-1930-4402-8d77-b2bebba308a3 48e6b7a6-50f5-4782-a5d4-53bb8f07e226 1 & powercfg -setactive scheme_current"))));

            l.Add(T(CAT_POWER, "Disable PCIe Link State Power Management",
                "Stops PCIe devices (GPU, NVMe) from entering low-power link states. Removes rare micro-stutter, small power cost.",
                RiskLevel.Moderate, true,
                log => log(Sh.RunCmd("powercfg -setacvalueindex scheme_current 501a4d13-42af-4429-9fd1-a8218c268e20 ee12f906-d277-404b-b6da-e5fa1a576df5 0 & powercfg -setactive scheme_current")),
                log => log(Sh.RunCmd("powercfg -setacvalueindex scheme_current 501a4d13-42af-4429-9fd1-a8218c268e20 ee12f906-d277-404b-b6da-e5fa1a576df5 1 & powercfg -setactive scheme_current"))));

            l.Add(T(CAT_POWER, "Disable hibernation",
                "Removes hiberfil.sys (frees several GB) and disables Fast Startup. Shutdown becomes a true full shutdown.",
                RiskLevel.Moderate, false,
                log => log(Sh.RunCmd("powercfg -h off")),
                log => log(Sh.RunCmd("powercfg -h on"))));

            l.Add(T(CAT_POWER, "Disable disk spindown && sleep on AC power",
                "Drive never spins down / PC never sleeps while plugged in. Good for a desktop gaming PC.",
                RiskLevel.Safe, true,
                log => { log(Sh.RunCmd("powercfg -change -disk-timeout-ac 0")); log(Sh.RunCmd("powercfg -change -standby-timeout-ac 0")); },
                log => { log(Sh.RunCmd("powercfg -change -disk-timeout-ac 20")); log(Sh.RunCmd("powercfg -change -standby-timeout-ac 30")); }));

            l.Add(T(CAT_POWER, "Disable adaptive brightness",
                "Stops automatic screen dimming based on ambient light/content (desktop monitors ignore this anyway; harmless).",
                RiskLevel.Safe, false,
                log => log(Sh.RunCmd("powercfg -setacvalueindex scheme_current sub_video videoadapt 0 & powercfg -setactive scheme_current")),
                log => log(Sh.RunCmd("powercfg -setacvalueindex scheme_current sub_video videoadapt 1 & powercfg -setactive scheme_current"))));
        }

        static void Network(List<Tweak> l)
        {
            l.Add(T(CAT_NET, "Disable Nagle's Algorithm (all interfaces)",
                "Sends small TCP packets immediately instead of buffering. Real: 1-10ms latency win in online games. Can slightly reduce bulk-transfer efficiency.",
                RiskLevel.Moderate, true,
                log => log(Sh.RunPS(
                    "$ifs = Get-ChildItem 'HKLM:\\SYSTEM\\CurrentControlSet\\Services\\Tcpip\\Parameters\\Interfaces'\r\n" +
                    "foreach ($i in $ifs) { Set-ItemProperty -Path $i.PSPath -Name TcpAckFrequency -Value 1 -Type DWord -EA SilentlyContinue; Set-ItemProperty -Path $i.PSPath -Name TCPNoDelay -Value 1 -Type DWord -EA SilentlyContinue }\r\n" +
                    "'done'")),
                log => log(Sh.RunPS(
                    "$ifs = Get-ChildItem 'HKLM:\\SYSTEM\\CurrentControlSet\\Services\\Tcpip\\Parameters\\Interfaces'\r\n" +
                    "foreach ($i in $ifs) { Remove-ItemProperty -Path $i.PSPath -Name TcpAckFrequency -EA SilentlyContinue; Remove-ItemProperty -Path $i.PSPath -Name TCPNoDelay -EA SilentlyContinue }\r\n" +
                    "'done'"))));

            l.Add(T(CAT_NET, "Disable network throttling index",
                "Removes the 10-packets/ms multimedia throttle so background network traffic doesn't get capped during gaming/streaming.",
                RiskLevel.Safe, true,
                log => log(Sh.Reg("add \"HKLM\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\Multimedia\\SystemProfile\" /v NetworkThrottlingIndex /t REG_DWORD /d 4294967295 /f")),
                log => log(Sh.Reg("delete \"HKLM\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\Multimedia\\SystemProfile\" /v NetworkThrottlingIndex /f"))));

            l.Add(T(CAT_NET, "TCP global tuning",
                "Sets RSS on, ECN/timestamps to sane defaults, CTCP congestion provider, fast retransmit timing. Matches known-good defaults.",
                RiskLevel.Safe, true,
                log => {
                    log(Sh.RunCmd("netsh int tcp set global autotuninglevel=normal"));
                    log(Sh.RunCmd("netsh int tcp set global rss=enabled"));
                    log(Sh.RunCmd("netsh int tcp set global congestionprovider=ctcp"));
                    log(Sh.RunCmd("netsh int tcp set global ecncapability=enabled"));
                    log(Sh.RunCmd("netsh int tcp set global timestamps=disabled"));
                    log(Sh.RunCmd("netsh int tcp set global initialRto=2000"));
                    log(Sh.RunCmd("netsh int tcp set global maxsynretransmissions=2"));
                },
                log => {
                    log(Sh.RunCmd("netsh int tcp set global autotuninglevel=normal"));
                    log(Sh.RunCmd("netsh int tcp set global rss=enabled"));
                    log(Sh.RunCmd("netsh int tcp set global congestionprovider=default"));
                    log(Sh.RunCmd("netsh int tcp set global ecncapability=default"));
                    log(Sh.RunCmd("netsh int tcp set global timestamps=disabled"));
                    log(Sh.RunCmd("netsh int tcp set global initialRto=3000"));
                }));

            l.Add(T(CAT_NET, "Remove 20% bandwidth QoS reservation",
                "Removes the legacy 'Windows reserves 20% of bandwidth' cap for QoS-tagged traffic (mostly a myth on modern Windows, but harmless to remove).",
                RiskLevel.Safe, false,
                log => log(Sh.Reg("add \"HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\Psched\" /v NonBestEffortLimit /t REG_DWORD /d 0 /f")),
                log => log(Sh.Reg("delete \"HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\Psched\" /v NonBestEffortLimit /f"))));

            l.Add(T(CAT_NET, "Set DNS to Cloudflare (1.1.1.1) on active adapters",
                "Overrides your router's DNS with Cloudflare on every 'Up' adapter. Faster domain lookups; no effect on in-game ping/FPS.",
                RiskLevel.Moderate, false,
                log => log(Sh.RunPS(
                    "$names = Get-NetAdapter | Where-Object Status -eq 'Up' | Select-Object -ExpandProperty Name\r\n" +
                    "foreach ($n in $names) { netsh interface ip set dns name=\"$n\" static 1.1.1.1; netsh interface ip add dns name=\"$n\" 1.0.0.1 index=2 }")),
                log => log(Sh.RunPS(
                    "$names = Get-NetAdapter | Where-Object Status -eq 'Up' | Select-Object -ExpandProperty Name\r\n" +
                    "foreach ($n in $names) { netsh interface ip set dns name=\"$n\" dhcp }"))));

            l.Add(T(CAT_NET, "Disable network adapter power management",
                "Stops Windows powering down NICs to save energy. Prevents random disconnects / latency spikes on wired and Wi-Fi adapters.",
                RiskLevel.Safe, true,
                log => log(Sh.RunPS("Get-NetAdapter | ForEach-Object { Disable-NetAdapterPowerManagement -Name $_.Name -EA SilentlyContinue }")),
                log => log(Sh.RunPS("Get-NetAdapter | ForEach-Object { Enable-NetAdapterPowerManagement -Name $_.Name -EA SilentlyContinue }"))));

            l.Add(T(CAT_NET, "Ethernet adapter advanced tuning (EEE / Interrupt Moderation / Flow Control)",
                "On physical Ethernet adapters only: disables Energy Efficient Ethernet and Interrupt Moderation (lower latency), disables Flow Control, enables RSS.",
                RiskLevel.Moderate, true,
                log => log(Sh.RunPS(
                    "$a = Get-NetAdapter -Physical | Where-Object {$_.MediaType -eq '802.3'}\r\n" +
                    "foreach ($x in $a) {\r\n" +
                    " Set-NetAdapterAdvancedProperty -Name $x.Name -DisplayName '*Energy Efficient Ethernet*' -DisplayValue 'Disabled' -EA SilentlyContinue\r\n" +
                    " Set-NetAdapterAdvancedProperty -Name $x.Name -DisplayName 'Interrupt Moderation' -DisplayValue 'Disabled' -EA SilentlyContinue\r\n" +
                    " Set-NetAdapterAdvancedProperty -Name $x.Name -DisplayName 'Flow Control' -DisplayValue 'Disabled' -EA SilentlyContinue\r\n" +
                    " Set-NetAdapterRss -Name $x.Name -Enabled $true -EA SilentlyContinue\r\n" +
                    "}")),
                log => log(Sh.RunPS(
                    "$a = Get-NetAdapter -Physical | Where-Object {$_.MediaType -eq '802.3'}\r\n" +
                    "foreach ($x in $a) {\r\n" +
                    " Set-NetAdapterAdvancedProperty -Name $x.Name -DisplayName '*Energy Efficient Ethernet*' -DisplayValue 'Enabled' -EA SilentlyContinue\r\n" +
                    " Set-NetAdapterAdvancedProperty -Name $x.Name -DisplayName 'Interrupt Moderation' -DisplayValue 'Enabled' -EA SilentlyContinue\r\n" +
                    " Set-NetAdapterAdvancedProperty -Name $x.Name -DisplayName 'Flow Control' -DisplayValue 'Enabled' -EA SilentlyContinue\r\n" +
                    "}"))));

            l.Add(T(CAT_NET, "Enable jumbo frames (9014 bytes)",
                "Larger Ethernet frames reduce per-packet overhead. ADVANCED: your router/switch must also support jumbo frames or you can lose connectivity.",
                RiskLevel.Advanced, false,
                log => log(Sh.RunPS("Get-NetAdapter -Physical | Where-Object {$_.MediaType -eq '802.3'} | ForEach-Object { Set-NetAdapterAdvancedProperty -Name $_.Name -DisplayName '*Jumbo*' -DisplayValue '9014 Bytes' -EA SilentlyContinue }")),
                log => log(Sh.RunPS("Get-NetAdapter -Physical | Where-Object {$_.MediaType -eq '802.3'} | ForEach-Object { Set-NetAdapterAdvancedProperty -Name $_.Name -DisplayName '*Jumbo*' -DisplayValue 'Disabled' -EA SilentlyContinue }"))));

            l.Add(T(CAT_NET, "Full network stack reset (backup + winsock/TCP/IP reset)",
                "ADVANCED / destructive: backs up current netsh + TCP/IP/AFD/DNS/QoS registry state next to this EXE, then fully resets Winsock, IPv4 and IPv6 stacks. Requires a reboot. Only use if your network feels broken.",
                RiskLevel.Advanced, false,
                log => {
                    string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Jewski_Network_Backup");
                    Directory.CreateDirectory(dir);
                    string stamp = DateTime.Now.ToString("yyyy-MM-dd_HHmmss");
                    log(Sh.RunCmd("netsh dump > \"" + Path.Combine(dir, "netsh_backup_" + stamp + ".txt") + "\""));
                    log(Sh.RunCmd("reg export \"HKLM\\SYSTEM\\CurrentControlSet\\Services\\Tcpip\\Parameters\" \"" + Path.Combine(dir, "tcpip_backup_" + stamp + ".reg") + "\" /y"));
                    log(Sh.RunCmd("reg export \"HKLM\\SYSTEM\\CurrentControlSet\\Services\\Tcpip6\\Parameters\" \"" + Path.Combine(dir, "tcpip6_backup_" + stamp + ".reg") + "\" /y"));
                    log(Sh.RunCmd("reg export \"HKLM\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\Multimedia\\SystemProfile\" \"" + Path.Combine(dir, "mm_backup_" + stamp + ".reg") + "\" /y"));
                    log(Sh.RunCmd("reg export \"HKLM\\SYSTEM\\CurrentControlSet\\Services\\AFD\\Parameters\" \"" + Path.Combine(dir, "afd_backup_" + stamp + ".reg") + "\" /y"));
                    log(Sh.RunCmd("reg export \"HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\Psched\" \"" + Path.Combine(dir, "psched_backup_" + stamp + ".reg") + "\" /y"));
                    log(Sh.RunCmd("reg export \"HKLM\\SYSTEM\\CurrentControlSet\\Services\\Dnscache\\Parameters\" \"" + Path.Combine(dir, "dns_backup_" + stamp + ".reg") + "\" /y"));
                    log(Sh.RunCmd("netsh winsock reset"));
                    log(Sh.RunCmd("netsh int ip reset"));
                    log(Sh.RunCmd("netsh int ipv6 reset"));
                    log("Backup saved to: " + dir + "  -- REBOOT REQUIRED.");
                },
                log => log("No automatic revert: restore the .reg files from Jewski_Network_Backup manually, then run 'netsh winsock reset' again and reboot.")));

            l.Add(T(CAT_NET, "Advanced TCP/IP registry tuning (legacy window/buffer values)",
                "ADVANCED, mostly PLACEBO: sets ~20 legacy TCP registry values (window sizes, connection limits, keep-alive, retransmit counts) from older tweak guides. Windows 10/11's TCP stack auto-tunes almost all of this already. Two values actively reduce built-in attack protection: SynAttackProtect=0 and EnableConnectionRateLimiting=0 disable SYN-flood / connection-rate defenses. Also sets DefaultTTL to 64 (from Windows' default 128), changing your OS's network fingerprint.",
                RiskLevel.Advanced, false,
                log => {
                    string k = "\"HKLM\\SYSTEM\\CurrentControlSet\\Services\\Tcpip\\Parameters\"";
                    var vals = new Dictionary<string, string> {
                        {"TcpTimedWaitDelay","30"},{"MaxUserPort","65534"},{"TcpNumConnections","16777214"},
                        {"MaxFreeTcbs","16000"},{"MaxHashTableSize","65536"},{"EnableTCPChimney","0"},
                        {"SackOpts","1"},{"Tcp1323Opts","1"},{"TcpWindowSize","65535"},{"GlobalMaxTcpWindowSize","16777215"},
                        {"KeepAliveInterval","1000"},{"KeepAliveTime","120000"},{"EnablePMTUDiscovery","1"},
                        {"EnablePMTUBHDetect","0"},{"DefaultTTL","64"},{"TcpMaxDataRetransmissions","3"},
                        {"SynAttackProtect","0"},{"TcpMaxDupAcks","2"},{"TcpFinWait2Delay","30"},{"EnableConnectionRateLimiting","0"}
                    };
                    foreach (var kv in vals) log(Sh.Reg("add " + k + " /v " + kv.Key + " /t REG_DWORD /d " + kv.Value + " /f"));
                },
                log => {
                    string k = "\"HKLM\\SYSTEM\\CurrentControlSet\\Services\\Tcpip\\Parameters\"";
                    foreach (var name in new[] { "TcpTimedWaitDelay","MaxUserPort","TcpNumConnections","MaxFreeTcbs","MaxHashTableSize","EnableTCPChimney","SackOpts","Tcp1323Opts","TcpWindowSize","GlobalMaxTcpWindowSize","KeepAliveInterval","KeepAliveTime","EnablePMTUDiscovery","EnablePMTUBHDetect","DefaultTTL","TcpMaxDataRetransmissions","SynAttackProtect","TcpMaxDupAcks","TcpFinWait2Delay","EnableConnectionRateLimiting" })
                        log(Sh.Reg("delete " + k + " /v " + name + " /f"));
                }));

            l.Add(T(CAT_NET, "Extended per-interface TCP tuning (WSD off, dead-gateway detection off)",
                "MODERATE: on top of the Nagle tweak, disables Web Services Discovery response and dead-gateway auto-failover per network interface, and shortens the initial RTT estimate. Marginal effect on most home connections.",
                RiskLevel.Moderate, false,
                log => log(Sh.RunPS(
                    "$ifs = Get-ChildItem 'HKLM:\\SYSTEM\\CurrentControlSet\\Services\\Tcpip\\Parameters\\Interfaces'\r\n" +
                    "foreach ($i in $ifs) { Set-ItemProperty -Path $i.PSPath -Name EnableWSD -Value 0 -Type DWord -EA SilentlyContinue; Set-ItemProperty -Path $i.PSPath -Name EnableDeadGWDetect -Value 0 -Type DWord -EA SilentlyContinue; Set-ItemProperty -Path $i.PSPath -Name TcpInitialRTT -Value 2 -Type DWord -EA SilentlyContinue }")),
                log => log(Sh.RunPS(
                    "$ifs = Get-ChildItem 'HKLM:\\SYSTEM\\CurrentControlSet\\Services\\Tcpip\\Parameters\\Interfaces'\r\n" +
                    "foreach ($i in $ifs) { Remove-ItemProperty -Path $i.PSPath -Name EnableWSD -EA SilentlyContinue; Remove-ItemProperty -Path $i.PSPath -Name EnableDeadGWDetect -EA SilentlyContinue; Remove-ItemProperty -Path $i.PSPath -Name TcpInitialRTT -EA SilentlyContinue }"))));

            l.Add(T(CAT_NET, "AFD extreme buffers (bursty asset downloads)",
                "ADVANCED, mostly placebo on modern Windows: raises AFD (Ancillary Function Driver) socket buffers and dynamic backlog limits. One real risk: DisableAddressSharing=1 can break apps/services that rely on shared socket ports (some VPN clients, ICS).",
                RiskLevel.Advanced, false,
                log => {
                    string k = "\"HKLM\\SYSTEM\\CurrentControlSet\\Services\\AFD\\Parameters\"";
                    var vals = new Dictionary<string, string> {
                        {"FastSendDatagramThreshold","1500"},{"FastCopyReceiveThreshold","1500"},
                        {"DefaultReceiveWindow","262144"},{"DefaultSendWindow","262144"},
                        {"DisableAddressSharing","1"},{"EnableDynamicBacklog","1"},{"MinimumDynamicBacklog","256"},
                        {"MaximumDynamicBacklog","32768"},{"DynamicBacklogGrowthDelta","128"},
                        {"DoNotHoldNicBuffers","1"},{"IgnorePushBitOnReceives","1"}
                    };
                    foreach (var kv in vals) log(Sh.Reg("add " + k + " /v " + kv.Key + " /t REG_DWORD /d " + kv.Value + " /f"));
                },
                log => {
                    string k = "\"HKLM\\SYSTEM\\CurrentControlSet\\Services\\AFD\\Parameters\"";
                    foreach (var name in new[] { "FastSendDatagramThreshold","FastCopyReceiveThreshold","DefaultReceiveWindow","DefaultSendWindow","DisableAddressSharing","EnableDynamicBacklog","MinimumDynamicBacklog","MaximumDynamicBacklog","DynamicBacklogGrowthDelta","DoNotHoldNicBuffers","IgnorePushBitOnReceives" })
                        log(Sh.Reg("delete " + k + " /v " + name + " /f"));
                }));

            l.Add(T(CAT_NET, "UDP + IPv4 stack tuning",
                "MODERATE, marginal effect: enables UDP receive-offload, IPv4 task offload, and raises the ARP neighbor cache / reassembly limits for busy connections.",
                RiskLevel.Moderate, false,
                log => {
                    log(Sh.RunCmd("netsh int udp set global uro=enabled"));
                    log(Sh.RunCmd("netsh int ipv4 set global taskoffload=enabled"));
                    log(Sh.RunCmd("netsh int ipv4 set global neighborcachelimit=16384"));
                    log(Sh.RunCmd("netsh int ipv4 set global reassemblylimit=33554432"));
                },
                log => {
                    log(Sh.RunCmd("netsh int udp set global uro=disabled"));
                    log(Sh.RunCmd("netsh int ipv4 set global taskoffload=enabled"));
                    log(Sh.RunCmd("netsh int ipv4 set global neighborcachelimit=8192"));
                }));

            l.Add(T(CAT_NET, "Aggressive DNS resolver cache tuning",
                "MODERATE: extends positive DNS cache lifetime but nearly eliminates negative-result caching, so a domain that briefly fails gets re-queried almost immediately. Slightly more DNS traffic in exchange for faster recovery from transient failures.",
                RiskLevel.Moderate, false,
                log => {
                    string k = "\"HKLM\\SYSTEM\\CurrentControlSet\\Services\\Dnscache\\Parameters\"";
                    log(Sh.Reg("add " + k + " /v MaxCacheEntryTtlLimit /t REG_DWORD /d 86400 /f"));
                    log(Sh.Reg("add " + k + " /v MaxNegativeCacheTtl /t REG_DWORD /d 1 /f"));
                    log(Sh.Reg("add " + k + " /v NegativeCacheTime /t REG_DWORD /d 1 /f"));
                    log(Sh.Reg("add " + k + " /v NetFailureCacheTime /t REG_DWORD /d 0 /f"));
                    log(Sh.Reg("add " + k + " /v NegativeSOACacheTime /t REG_DWORD /d 1 /f"));
                    log(Sh.Reg("add " + k + " /v MaxCacheTtl /t REG_DWORD /d 86400 /f"));
                },
                log => {
                    string k = "\"HKLM\\SYSTEM\\CurrentControlSet\\Services\\Dnscache\\Parameters\"";
                    foreach (var name in new[] { "MaxCacheEntryTtlLimit","MaxNegativeCacheTtl","NegativeCacheTime","NetFailureCacheTime","NegativeSOACacheTime","MaxCacheTtl" })
                        log(Sh.Reg("delete " + k + " /v " + name + " /f"));
                }));

            l.Add(T(CAT_NET, "Delivery Optimization: foreground-only (cap P2P upload)",
                "MODERATE: keeps Delivery Optimization running but disables peer-to-peer sharing and caps background bandwidth to 5%, leaving 100% for foreground/your own traffic. A lighter alternative to fully disabling the DoSvc service.",
                RiskLevel.Moderate, false,
                log => {
                    log(Sh.Reg("add \"HKLM\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\DeliveryOptimization\\Config\" /v DODownloadMode /t REG_DWORD /d 0 /f"));
                    log(Sh.Reg("add \"HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\DeliveryOptimization\" /v DODownloadMode /t REG_DWORD /d 0 /f"));
                    log(Sh.Reg("add \"HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\DeliveryOptimization\" /v DOPercentageMaxBackgroundBandwidth /t REG_DWORD /d 5 /f"));
                    log(Sh.Reg("add \"HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\DeliveryOptimization\" /v DOPercentageMaxForegroundBandwidth /t REG_DWORD /d 100 /f"));
                },
                log => {
                    log(Sh.Reg("delete \"HKLM\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\DeliveryOptimization\\Config\" /v DODownloadMode /f"));
                    log(Sh.Reg("delete \"HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\DeliveryOptimization\" /v DODownloadMode /f"));
                    log(Sh.Reg("delete \"HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\DeliveryOptimization\" /v DOPercentageMaxBackgroundBandwidth /f"));
                    log(Sh.Reg("delete \"HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\DeliveryOptimization\" /v DOPercentageMaxForegroundBandwidth /f"));
                }));

            l.Add(T(CAT_NET, "Ethernet: extended low-latency NIC properties",
                "ADVANCED: pushes ~20 advanced adapter properties at once (disables LSO/LRO offloads, Green Ethernet/EEE, wake-on-LAN; forces RSS to 8 queues, bigger Rx/Tx buffers, no jumbo/priority-VLAN tagging). Silently skips any property your specific NIC doesn't expose. Disabling send/receive offloads trades a little extra CPU load for more consistent per-packet latency — a real but contested trade, not a free win. Speed/Duplex is deliberately left on Auto-Negotiate (forcing it is a common cause of dropped links).",
                RiskLevel.Advanced, false,
                log => log(Sh.RunPS(
                    "$eth = Get-NetAdapter -Physical | Where-Object { $_.InterfaceDescription -notlike '*Wi-Fi*' -and $_.InterfaceDescription -notlike '*Wireless*' -and $_.InterfaceDescription -notlike '*802.11*' }\r\n" +
                    "foreach ($a in $eth) {\r\n" +
                    " Disable-NetAdapterPowerManagement -Name $a.Name -EA SilentlyContinue\r\n" +
                    " Disable-NetAdapterRsc -Name $a.Name -EA SilentlyContinue\r\n" +
                    " Disable-NetAdapterLso -Name $a.Name -EA SilentlyContinue\r\n" +
                    " try { Set-NetAdapterRss -Name $a.Name -Enabled $true -NumberOfReceiveQueues 8 -EA Stop } catch { Set-NetAdapterRss -Name $a.Name -Enabled $true -EA SilentlyContinue }\r\n" +
                    " $props = @{'Energy Efficient Ethernet'='Disabled';'Interrupt Moderation'='Disabled';'Interrupt Moderation Rate'='Off';'Flow Control'='Disabled';'Receive Buffers'='4096';'Transmit Buffers'='4096';'Large Send Offload v2 (IPv4)'='Disabled';'Large Send Offload v2 (IPv6)'='Disabled';'Large Receive Offload (IPv4)'='Disabled';'Large Receive Offload (IPv6)'='Disabled';'Wake on Magic Packet'='Disabled';'Wake on Pattern Match'='Disabled';'Green Ethernet'='Disabled';'Advanced EEE'='Disabled';'EEE'='Disabled';'Ultra Low Power Mode'='Disabled';'Jumbo Packet'='Disabled';'Priority & VLAN'='Priority & VLAN Disabled'}\r\n" +
                    " foreach ($k in $props.Keys) { Set-NetAdapterAdvancedProperty -Name $a.Name -DisplayName $k -DisplayValue $props[$k] -EA SilentlyContinue }\r\n" +
                    "}")),
                log => log(Sh.RunPS(
                    "$eth = Get-NetAdapter -Physical | Where-Object { $_.InterfaceDescription -notlike '*Wi-Fi*' -and $_.InterfaceDescription -notlike '*Wireless*' }\r\n" +
                    "foreach ($a in $eth) {\r\n" +
                    " Enable-NetAdapterPowerManagement -Name $a.Name -EA SilentlyContinue\r\n" +
                    " Enable-NetAdapterRsc -Name $a.Name -EA SilentlyContinue\r\n" +
                    " Enable-NetAdapterLso -Name $a.Name -EA SilentlyContinue\r\n" +
                    " $props = @{'Energy Efficient Ethernet'='Enabled';'Interrupt Moderation'='Enabled';'Flow Control'='Rx & Tx Enabled';'Large Send Offload v2 (IPv4)'='Enabled';'Large Send Offload v2 (IPv6)'='Enabled'}\r\n" +
                    " foreach ($k in $props.Keys) { Set-NetAdapterAdvancedProperty -Name $a.Name -DisplayName $k -DisplayValue $props[$k] -EA SilentlyContinue }\r\n" +
                    "}"))));

            l.Add(T(CAT_NET, "Ethernet: set interface metric to 1 (routing priority)",
                "Locks your wired adapter as the highest-priority route so it's always preferred over Wi-Fi or any virtual adapter. Real and safe when multiple network adapters are present.",
                RiskLevel.Safe, true,
                log => log(Sh.RunPS(
                    "$eth = Get-NetAdapter -Physical | Where-Object { $_.InterfaceDescription -notlike '*Wi-Fi*' -and $_.InterfaceDescription -notlike '*Wireless*' -and ($_.MediaType -eq '802.3' -or $_.InterfaceDescription -like '*Ethernet*') }\r\n" +
                    "foreach ($a in $eth) { Set-NetIPInterface -InterfaceAlias $a.Name -InterfaceMetric 1 -EA SilentlyContinue }")),
                log => log(Sh.RunPS(
                    "$eth = Get-NetAdapter -Physical | Where-Object { $_.InterfaceDescription -notlike '*Wi-Fi*' -and $_.InterfaceDescription -notlike '*Wireless*' }\r\n" +
                    "foreach ($a in $eth) { Set-NetIPInterface -InterfaceAlias $a.Name -AutomaticMetric Enabled -EA SilentlyContinue }"))));

            l.Add(T(CAT_NET, "Delivery Optimization: LAN peers only (no internet upload)",
                "MODERATE, middle ground: keeps peer-to-peer update sharing but restricts it to your local network only — no uploading to strangers on the internet, still gets local-network speed benefits if you have multiple Windows PCs at home.",
                RiskLevel.Moderate, false,
                log => log(Sh.Reg("add \"HKLM\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\DeliveryOptimization\\Config\" /v DODownloadMode /t REG_DWORD /d 1 /f")),
                log => log(Sh.Reg("delete \"HKLM\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\DeliveryOptimization\\Config\" /v DODownloadMode /f"))));

            l.Add(T(CAT_NET, "Enable DNS-over-HTTPS (Cloudflare)",
                "MODERATE: encrypts DNS lookups so your ISP/router can't see plaintext domain queries. Requires Windows 10 2004+ (you have 22H2, so it's supported) and works best paired with the Cloudflare DNS tweak above.",
                RiskLevel.Moderate, false,
                log => {
                    log(Sh.Reg("add \"HKLM\\SYSTEM\\CurrentControlSet\\Services\\Dnscache\\Parameters\" /v EnableAutoDoh /t REG_DWORD /d 2 /f"));
                    log(Sh.RunPS("Set-DnsClientDohServerAddress -ServerAddress 1.1.1.1 -DohTemplate 'https://cloudflare-dns.com/dns-query' -AllowFallbackToUdp $true -AutoUpgrade $true -EA SilentlyContinue"));
                },
                log => {
                    log(Sh.Reg("delete \"HKLM\\SYSTEM\\CurrentControlSet\\Services\\Dnscache\\Parameters\" /v EnableAutoDoh /f"));
                }));

            l.Add(T(CAT_NET, "IRQ priority registry values (legacy, no effect)",
                "PLACEBO on Windows 10/11: the ACPI HAL ignores IRQ8Priority/IRQ16-23Priority on modern systems. Included only for completeness / parity with older tweak guides — expect zero measurable change.",
                RiskLevel.Safe, false,
                log => {
                    string k = "\"HKLM\\SYSTEM\\CurrentControlSet\\Control\\PriorityControl\"";
                    log(Sh.Reg("add " + k + " /v IRQ8Priority /t REG_DWORD /d 1 /f"));
                    foreach (var n in new[] { "16", "17", "18", "19", "20", "21", "22", "23" })
                        log(Sh.Reg("add " + k + " /v IRQ" + n + "Priority /t REG_DWORD /d 8 /f"));
                },
                log => {
                    string k = "\"HKLM\\SYSTEM\\CurrentControlSet\\Control\\PriorityControl\"";
                    log(Sh.Reg("delete " + k + " /v IRQ8Priority /f"));
                    foreach (var n in new[] { "16", "17", "18", "19", "20", "21", "22", "23" })
                        log(Sh.Reg("delete " + k + " /v IRQ" + n + "Priority /f"));
                }));
        }

        static void Audio(List<Tweak> l)
        {
            l.Add(T(CAT_AUDIO, "Disable audio enhancements (all playback devices)",
                "Turns off driver-level 'audio enhancements' processing on every render device. Real: removes a small, sometimes-audible processing latency and occasional enhancement-related crackle.",
                RiskLevel.Safe, true,
                log => log(Sh.RunPS("Get-ItemProperty -Path 'HKLM:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\MMDevices\\Audio\\Render\\*\\Properties' -EA SilentlyContinue | ForEach-Object { reg add $_.PSPath /v \"{1da5d803-d492-4edd-8c23-e0c0ffee7f0e},1\" /t REG_DWORD /d 0 /f }")),
                log => log(Sh.RunPS("Get-ItemProperty -Path 'HKLM:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\MMDevices\\Audio\\Render\\*\\Properties' -EA SilentlyContinue | ForEach-Object { reg delete $_.PSPath /v \"{1da5d803-d492-4edd-8c23-e0c0ffee7f0e},1\" /f }"))));

            l.Add(T(CAT_AUDIO, "Disable audio device power saving",
                "Stops Windows power-cycling audio endpoints. Fixes the common pop/click-on-resume issue with some DACs/USB audio devices.",
                RiskLevel.Safe, true,
                log => log(Sh.RunPS("Get-PnpDevice -Class AudioEndpoint -EA SilentlyContinue | ForEach-Object { try { $p = 'HKLM:\\SYSTEM\\CurrentControlSet\\Enum\\' + $_.InstanceId + '\\Device Parameters\\Power'; if (!(Test-Path $p)) { New-Item -Path $p -Force | Out-Null }; Set-ItemProperty -Path $p -Name ConservationIdleTime -Value 0 -Type DWord -EA SilentlyContinue } catch {} }")),
                log => log(Sh.RunPS("Get-PnpDevice -Class AudioEndpoint -EA SilentlyContinue | ForEach-Object { try { $p = 'HKLM:\\SYSTEM\\CurrentControlSet\\Enum\\' + $_.InstanceId + '\\Device Parameters\\Power'; Remove-ItemProperty -Path $p -Name ConservationIdleTime -EA SilentlyContinue } catch {} }"))));

            l.Add(T(CAT_AUDIO, "Boost Pro Audio scheduler priority",
                "Raises the multimedia scheduler's 'Pro Audio' task category priority. Only matters if you use a dedicated audio interface/DAW; harmless otherwise.",
                RiskLevel.Moderate, false,
                log => {
                    string k = "\"HKLM\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\Multimedia\\SystemProfile\\Tasks\\Pro Audio\"";
                    log(Sh.Reg("add " + k + " /v \"Scheduling Category\" /t REG_SZ /d High /f"));
                    log(Sh.Reg("add " + k + " /v \"SFIO Priority\" /t REG_SZ /d High /f"));
                    log(Sh.Reg("add " + k + " /v \"Priority\" /t REG_DWORD /d 6 /f"));
                    log(Sh.Reg("add " + k + " /v \"GPU Priority\" /t REG_DWORD /d 8 /f"));
                },
                log => {
                    string k = "\"HKLM\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\Multimedia\\SystemProfile\\Tasks\\Pro Audio\"";
                    log(Sh.Reg("add " + k + " /v \"Scheduling Category\" /t REG_SZ /d Medium /f"));
                    log(Sh.Reg("add " + k + " /v \"SFIO Priority\" /t REG_SZ /d Normal /f"));
                    log(Sh.Reg("add " + k + " /v \"Priority\" /t REG_DWORD /d 2 /f"));
                }));
        }

        static void Input(List<Tweak> l)
        {
            l.Add(T(CAT_INPUT, "Disable mouse acceleration (raw 1:1 input)",
                "Sets a flat response curve — no 'Enhance pointer precision'. Standard competitive-gaming setting.",
                RiskLevel.Safe, true,
                log => {
                    log(Sh.Reg("add \"HKCU\\Control Panel\\Mouse\" /v MouseSpeed /t REG_SZ /d 0 /f"));
                    log(Sh.Reg("add \"HKCU\\Control Panel\\Mouse\" /v MouseThreshold1 /t REG_SZ /d 0 /f"));
                    log(Sh.Reg("add \"HKCU\\Control Panel\\Mouse\" /v MouseThreshold2 /t REG_SZ /d 0 /f"));
                    log(Sh.Reg("add \"HKCU\\Control Panel\\Mouse\" /v SmoothMouseXCurve /t REG_BINARY /d 000000000000000000000000000000000000000000000000 /f"));
                    log(Sh.Reg("add \"HKCU\\Control Panel\\Mouse\" /v SmoothMouseYCurve /t REG_BINARY /d 000000000000000000000000000000000000000000000000 /f"));
                },
                log => {
                    log(Sh.Reg("add \"HKCU\\Control Panel\\Mouse\" /v MouseSpeed /t REG_SZ /d 1 /f"));
                    log(Sh.Reg("add \"HKCU\\Control Panel\\Mouse\" /v MouseThreshold1 /t REG_SZ /d 6 /f"));
                    log(Sh.Reg("add \"HKCU\\Control Panel\\Mouse\" /v MouseThreshold2 /t REG_SZ /d 10 /f"));
                    log(Sh.Reg("delete \"HKCU\\Control Panel\\Mouse\" /v SmoothMouseXCurve /f"));
                    log(Sh.Reg("delete \"HKCU\\Control Panel\\Mouse\" /v SmoothMouseYCurve /f"));
                }));

            l.Add(T(CAT_INPUT, "Increase mouse/keyboard data queue size",
                "Raises the input buffer so fast clicks/high-poll-rate mice and rapid key presses don't get dropped.",
                RiskLevel.Safe, true,
                log => {
                    log(Sh.Reg("add \"HKLM\\SYSTEM\\CurrentControlSet\\Services\\mouclass\\Parameters\" /v MouseDataQueueSize /t REG_DWORD /d 20 /f"));
                    log(Sh.Reg("add \"HKLM\\SYSTEM\\CurrentControlSet\\Services\\kbdclass\\Parameters\" /v KeyboardDataQueueSize /t REG_DWORD /d 20 /f"));
                },
                log => {
                    log(Sh.Reg("add \"HKLM\\SYSTEM\\CurrentControlSet\\Services\\mouclass\\Parameters\" /v MouseDataQueueSize /t REG_DWORD /d 100 /f"));
                    log(Sh.Reg("add \"HKLM\\SYSTEM\\CurrentControlSet\\Services\\kbdclass\\Parameters\" /v KeyboardDataQueueSize /t REG_DWORD /d 100 /f"));
                }));

            l.Add(T(CAT_INPUT, "Fast hover / double-click / pointer preferences",
                "Personal preference, not a performance tweak: shorter hover delay, faster double-click, no pointer trails.",
                RiskLevel.Safe, false,
                log => {
                    log(Sh.Reg("add \"HKCU\\Control Panel\\Mouse\" /v DoubleClickSpeed /t REG_SZ /d 300 /f"));
                    log(Sh.Reg("add \"HKCU\\Control Panel\\Mouse\" /v MouseHoverTime /t REG_SZ /d 100 /f"));
                    log(Sh.Reg("add \"HKCU\\Control Panel\\Mouse\" /v MouseTrails /t REG_SZ /d 0 /f"));
                },
                log => {
                    log(Sh.Reg("add \"HKCU\\Control Panel\\Mouse\" /v DoubleClickSpeed /t REG_SZ /d 500 /f"));
                    log(Sh.Reg("add \"HKCU\\Control Panel\\Mouse\" /v MouseHoverTime /t REG_SZ /d 400 /f"));
                    log(Sh.Reg("add \"HKCU\\Control Panel\\Mouse\" /v MouseTrails /t REG_SZ /d 0 /f"));
                }));

            l.Add(T(CAT_INPUT, "Fastest keyboard repeat rate",
                "Personal preference: minimum repeat delay, maximum repeat speed.",
                RiskLevel.Safe, false,
                log => { log(Sh.Reg("add \"HKCU\\Control Panel\\Keyboard\" /v KeyboardDelay /t REG_SZ /d 0 /f")); log(Sh.Reg("add \"HKCU\\Control Panel\\Keyboard\" /v KeyboardSpeed /t REG_SZ /d 31 /f")); },
                log => { log(Sh.Reg("add \"HKCU\\Control Panel\\Keyboard\" /v KeyboardDelay /t REG_SZ /d 1 /f")); log(Sh.Reg("add \"HKCU\\Control Panel\\Keyboard\" /v KeyboardSpeed /t REG_SZ /d 31 /f")); }));
        }

        static void Visuals(List<Tweak> l)
        {
            l.Add(T(CAT_VISUAL, "Visual effects: Best Performance",
                "Same as System Properties > Performance > 'Adjust for best performance'. Disables shadows/animations/thumbnails.",
                RiskLevel.Safe, true,
                log => log(Sh.Reg("add \"HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\VisualEffects\" /v VisualFXSetting /t REG_DWORD /d 2 /f")),
                log => log(Sh.Reg("add \"HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\VisualEffects\" /v VisualFXSetting /t REG_DWORD /d 0 /f"))));

            l.Add(T(CAT_VISUAL, "Disable animations && transparency",
                "Removes taskbar/window animations and Start/taskbar acrylic blur. Purely cosmetic snappiness, near-zero FPS effect.",
                RiskLevel.Safe, true,
                log => {
                    log(Sh.Reg("add \"HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced\" /v TaskbarAnimations /t REG_DWORD /d 0 /f"));
                    log(Sh.Reg("add \"HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize\" /v EnableTransparency /t REG_DWORD /d 0 /f"));
                    log(Sh.Reg("add \"HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced\" /v EnableTransparency /t REG_DWORD /d 0 /f"));
                },
                log => {
                    log(Sh.Reg("add \"HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced\" /v TaskbarAnimations /t REG_DWORD /d 1 /f"));
                    log(Sh.Reg("add \"HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize\" /v EnableTransparency /t REG_DWORD /d 1 /f"));
                    log(Sh.Reg("add \"HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced\" /v EnableTransparency /t REG_DWORD /d 1 /f"));
                }));

            l.Add(T(CAT_VISUAL, "Zero menu/window animation delay",
                "Preference: instant menus, no window-drag ghosting, no minimize/maximize animation.",
                RiskLevel.Safe, false,
                log => {
                    log(Sh.Reg("add \"HKCU\\Control Panel\\Desktop\" /v MenuShowDelay /t REG_SZ /d 0 /f"));
                    log(Sh.Reg("add \"HKCU\\Control Panel\\Desktop\" /v DragFullWindows /t REG_SZ /d 0 /f"));
                    log(Sh.Reg("add \"HKCU\\Control Panel\\Desktop\\WindowMetrics\" /v MinAnimate /t REG_SZ /d 0 /f"));
                },
                log => {
                    log(Sh.Reg("add \"HKCU\\Control Panel\\Desktop\" /v MenuShowDelay /t REG_SZ /d 400 /f"));
                    log(Sh.Reg("add \"HKCU\\Control Panel\\Desktop\" /v DragFullWindows /t REG_SZ /d 1 /f"));
                    log(Sh.Reg("add \"HKCU\\Control Panel\\Desktop\\WindowMetrics\" /v MinAnimate /t REG_SZ /d 1 /f"));
                }));

            l.Add(T(CAT_VISUAL, "Disable thumbnail hover delay",
                "File Explorer/taskbar thumbnail previews pop up instantly.",
                RiskLevel.Safe, false,
                log => log(Sh.Reg("add \"HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced\" /v ExtendedUIHoverTime /t REG_DWORD /d 1 /f")),
                log => log(Sh.Reg("delete \"HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced\" /v ExtendedUIHoverTime /f"))));

            l.Add(T(CAT_VISUAL, "Disable balloon tips && startup sound",
                "Quiets Windows notification balloons and the login chime.",
                RiskLevel.Safe, false,
                log => {
                    log(Sh.Reg("add \"HKCU\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced\" /v EnableBalloonTips /t REG_DWORD /d 0 /f"));
                    log(Sh.Reg("add \"HKLM\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Policies\\System\" /v DisableStartupSound /t REG_DWORD /d 1 /f"));
                },
                log => {
                    log(Sh.Reg("add \"HKCU\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced\" /v EnableBalloonTips /t REG_DWORD /d 1 /f"));
                    log(Sh.Reg("add \"HKLM\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Policies\\System\" /v DisableStartupSound /t REG_DWORD /d 0 /f"));
                }));

            l.Add(T(CAT_VISUAL, "Hide taskbar Widgets / Chat / Meet Now / Task View",
                "Preference: removes taskbar clutter buttons.",
                RiskLevel.Safe, false,
                log => {
                    log(Sh.Reg("add \"HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced\" /v TaskbarDa /t REG_DWORD /d 0 /f"));
                    log(Sh.Reg("add \"HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced\" /v TaskbarMn /t REG_DWORD /d 0 /f"));
                    log(Sh.Reg("add \"HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced\" /v HideSCAMeetNow /t REG_DWORD /d 1 /f"));
                    log(Sh.Reg("add \"HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced\" /v ShowTaskViewButton /t REG_DWORD /d 0 /f"));
                },
                log => {
                    log(Sh.Reg("add \"HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced\" /v TaskbarDa /t REG_DWORD /d 1 /f"));
                    log(Sh.Reg("add \"HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced\" /v TaskbarMn /t REG_DWORD /d 1 /f"));
                    log(Sh.Reg("add \"HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced\" /v HideSCAMeetNow /t REG_DWORD /d 0 /f"));
                    log(Sh.Reg("add \"HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced\" /v ShowTaskViewButton /t REG_DWORD /d 1 /f"));
                }));
        }

        static void Privacy(List<Tweak> l)
        {
            l.Add(T(CAT_PRIVACY, "Telemetry to minimum + disable feedback/error reporting",
                "Sets diagnostic data to the lowest allowed level, turns off feedback nags and Windows Error Reporting.",
                RiskLevel.Moderate, true,
                log => {
                    log(Sh.Reg("add \"HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\DataCollection\" /v AllowTelemetry /t REG_DWORD /d 0 /f"));
                    log(Sh.Reg("add \"HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\DataCollection\" /v DoNotShowFeedbackNotifications /t REG_DWORD /d 1 /f"));
                    log(Sh.Reg("add \"HKCU\\SOFTWARE\\Microsoft\\Siuf\\Rules\" /v NumberOfSIUFInPeriod /t REG_DWORD /d 0 /f"));
                    log(Sh.Reg("add \"HKLM\\SOFTWARE\\Microsoft\\Windows\\Windows Error Reporting\" /v Disabled /t REG_DWORD /d 1 /f"));
                },
                log => {
                    log(Sh.Reg("add \"HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\DataCollection\" /v AllowTelemetry /t REG_DWORD /d 3 /f"));
                    log(Sh.Reg("delete \"HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\DataCollection\" /v DoNotShowFeedbackNotifications /f"));
                    log(Sh.Reg("delete \"HKCU\\SOFTWARE\\Microsoft\\Siuf\\Rules\" /v NumberOfSIUFInPeriod /f"));
                    log(Sh.Reg("add \"HKLM\\SOFTWARE\\Microsoft\\Windows\\Windows Error Reporting\" /v Disabled /t REG_DWORD /d 0 /f"));
                }));

            l.Add(T(CAT_PRIVACY, "Disable Advertising ID",
                "Stops apps from being given a unique ad-tracking ID for this Windows user.",
                RiskLevel.Safe, true,
                log => { log(Sh.Reg("add \"HKCU\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\AdvertisingInfo\" /v Enabled /t REG_DWORD /d 0 /f")); log(Sh.Reg("add \"HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\AdvertisingInfo\" /v DisabledByGroupPolicy /t REG_DWORD /d 1 /f")); },
                log => { log(Sh.Reg("add \"HKCU\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\AdvertisingInfo\" /v Enabled /t REG_DWORD /d 1 /f")); log(Sh.Reg("delete \"HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\AdvertisingInfo\" /v DisabledByGroupPolicy /f")); }));

            l.Add(T(CAT_PRIVACY, "Disable Activity Feed / Timeline",
                "Stops Windows publishing/uploading your app activity history.",
                RiskLevel.Safe, true,
                log => { log(Sh.Reg("add \"HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\System\" /v EnableActivityFeed /t REG_DWORD /d 0 /f")); log(Sh.Reg("add \"HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\System\" /v PublishUserActivities /t REG_DWORD /d 0 /f")); log(Sh.Reg("add \"HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\System\" /v UploadUserActivities /t REG_DWORD /d 0 /f")); },
                log => { log(Sh.Reg("add \"HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\System\" /v EnableActivityFeed /t REG_DWORD /d 1 /f")); log(Sh.Reg("add \"HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\System\" /v PublishUserActivities /t REG_DWORD /d 1 /f")); log(Sh.Reg("add \"HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\System\" /v UploadUserActivities /t REG_DWORD /d 1 /f")); }));

            l.Add(T(CAT_PRIVACY, "Disable tailored experiences / Spotlight",
                "Stops Windows using your diagnostic data to personalize tips and lock-screen Spotlight ads.",
                RiskLevel.Safe, true,
                log => { log(Sh.Reg("add \"HKCU\\SOFTWARE\\Policies\\Microsoft\\Windows\\CloudContent\" /v DisableTailoredExperiencesWithDiagnosticData /t REG_DWORD /d 1 /f")); log(Sh.Reg("add \"HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\CloudContent\" /v DisableWindowsSpotlightFeatures /t REG_DWORD /d 1 /f")); },
                log => { log(Sh.Reg("delete \"HKCU\\SOFTWARE\\Policies\\Microsoft\\Windows\\CloudContent\" /v DisableTailoredExperiencesWithDiagnosticData /f")); log(Sh.Reg("delete \"HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\CloudContent\" /v DisableWindowsSpotlightFeatures /f")); }));

            l.Add(T(CAT_PRIVACY, "Disable Find My Device",
                "Turns off Windows' device location-tracking feature.",
                RiskLevel.Safe, true,
                log => log(Sh.Reg("add \"HKLM\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\FindMyDevice\" /v LocationSyncEnabled /t REG_DWORD /d 0 /f")),
                log => log(Sh.Reg("add \"HKLM\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\FindMyDevice\" /v LocationSyncEnabled /t REG_DWORD /d 1 /f"))));

            l.Add(T(CAT_PRIVACY, "Disable typing/inking data collection",
                "Stops Windows learning from your keystrokes/handwriting for autocomplete personalization.",
                RiskLevel.Safe, true,
                log => { log(Sh.Reg("add \"HKCU\\SOFTWARE\\Microsoft\\InputPersonalization\" /v RestrictImplicitInkCollection /t REG_DWORD /d 1 /f")); log(Sh.Reg("add \"HKCU\\SOFTWARE\\Microsoft\\InputPersonalization\" /v RestrictImplicitTextCollection /t REG_DWORD /d 1 /f")); },
                log => { log(Sh.Reg("add \"HKCU\\SOFTWARE\\Microsoft\\InputPersonalization\" /v RestrictImplicitInkCollection /t REG_DWORD /d 0 /f")); log(Sh.Reg("add \"HKCU\\SOFTWARE\\Microsoft\\InputPersonalization\" /v RestrictImplicitTextCollection /t REG_DWORD /d 0 /f")); }));

            l.Add(T(CAT_PRIVACY, "Disable voice-activation always-listening",
                "Stops apps from keeping the microphone hot for wake-word detection.",
                RiskLevel.Safe, true,
                log => log(Sh.Reg("add \"HKCU\\SOFTWARE\\Microsoft\\Speech_OneCore\\Settings\\VoiceActivation\\UserPreferenceForAllApps\" /v AgentActivationEnabled /t REG_DWORD /d 0 /f")),
                log => log(Sh.Reg("add \"HKCU\\SOFTWARE\\Microsoft\\Speech_OneCore\\Settings\\VoiceActivation\\UserPreferenceForAllApps\" /v AgentActivationEnabled /t REG_DWORD /d 1 /f"))));

            l.Add(T(CAT_PRIVACY, "Disable clipboard cloud sync/history",
                "Turns off cross-device clipboard sync and the Win+V clipboard history panel.",
                RiskLevel.Safe, false,
                log => { log(Sh.Reg("add \"HKCU\\SOFTWARE\\Microsoft\\Clipboard\" /v EnableClipboardHistory /t REG_DWORD /d 0 /f")); log(Sh.Reg("add \"HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\System\" /v AllowCrossDeviceClipboard /t REG_DWORD /d 0 /f")); },
                log => { log(Sh.Reg("add \"HKCU\\SOFTWARE\\Microsoft\\Clipboard\" /v EnableClipboardHistory /t REG_DWORD /d 1 /f")); log(Sh.Reg("delete \"HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\System\" /v AllowCrossDeviceClipboard /f")); }));

            l.Add(T(CAT_PRIVACY, "App privacy lockdown (camera/mic/location/background = deny)",
                "ADVANCED: force-denies apps' access to camera, microphone, location and background running at the POLICY level. Can break Discord/Zoom/UWP camera+mic apps and conflicts with the 'Restore Microphone Access' fix in the Fixes tab.",
                RiskLevel.Advanced, false,
                log => {
                    log(Sh.Reg("add \"HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\AppPrivacy\" /v LetAppsAccessLocation /t REG_DWORD /d 2 /f"));
                    log(Sh.Reg("add \"HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\AppPrivacy\" /v LetAppsAccessCamera /t REG_DWORD /d 2 /f"));
                    log(Sh.Reg("add \"HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\AppPrivacy\" /v LetAppsAccessMicrophone /t REG_DWORD /d 2 /f"));
                    log(Sh.Reg("add \"HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\AppPrivacy\" /v LetAppsRunInBackground /t REG_DWORD /d 2 /f"));
                },
                log => {
                    log(Sh.Reg("delete \"HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\AppPrivacy\" /v LetAppsAccessLocation /f"));
                    log(Sh.Reg("delete \"HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\AppPrivacy\" /v LetAppsAccessCamera /f"));
                    log(Sh.Reg("delete \"HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\AppPrivacy\" /v LetAppsAccessMicrophone /f"));
                    log(Sh.Reg("delete \"HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\AppPrivacy\" /v LetAppsRunInBackground /f"));
                }));

            l.Add(T(CAT_PRIVACY, "Block Microsoft telemetry domains via hosts file",
                "ADVANCED: adds 0.0.0.0 entries for known telemetry domains to the hosts file. Blunt instrument; some anti-cheat/security tools flag hosts-file edits.",
                RiskLevel.Advanced, false,
                log => log(Sh.RunPS(
                    "$p = 'C:\\Windows\\System32\\drivers\\etc\\hosts'\r\n" +
                    "$hosts = Get-Content $p\r\n" +
                    "$block = @('0.0.0.0 vortex.data.microsoft.com','0.0.0.0 settings-win.data.microsoft.com','0.0.0.0 telemetry.microsoft.com','0.0.0.0 watson.telemetry.microsoft.com','0.0.0.0 reports.wes.df.telemetry.microsoft.com')\r\n" +
                    "foreach ($b in $block) { if ($hosts -notcontains $b) { Add-Content $p $b } }")),
                log => log(Sh.RunPS(
                    "$p = 'C:\\Windows\\System32\\drivers\\etc\\hosts'\r\n" +
                    "$block = @('0.0.0.0 vortex.data.microsoft.com','0.0.0.0 settings-win.data.microsoft.com','0.0.0.0 telemetry.microsoft.com','0.0.0.0 watson.telemetry.microsoft.com','0.0.0.0 reports.wes.df.telemetry.microsoft.com')\r\n" +
                    "(Get-Content $p) | Where-Object { $block -notcontains $_ } | Set-Content $p"))));
        }

        static void Gaming(List<Tweak> l)
        {
            l.Add(T(CAT_GAMING, "Enable Windows Game Mode",
                "Deprioritizes background scheduler/driver work while a game runs fullscreen.",
                RiskLevel.Safe, true,
                log => { log(Sh.Reg("add \"HKCU\\SOFTWARE\\Microsoft\\GameBar\" /v AllowAutoGameMode /t REG_DWORD /d 1 /f")); log(Sh.Reg("add \"HKCU\\SOFTWARE\\Microsoft\\GameBar\" /v AutoGameModeEnabled /t REG_DWORD /d 1 /f")); },
                log => { log(Sh.Reg("add \"HKCU\\SOFTWARE\\Microsoft\\GameBar\" /v AllowAutoGameMode /t REG_DWORD /d 0 /f")); log(Sh.Reg("add \"HKCU\\SOFTWARE\\Microsoft\\GameBar\" /v AutoGameModeEnabled /t REG_DWORD /d 0 /f")); }));

            l.Add(T(CAT_GAMING, "Enable Hardware-Accelerated GPU Scheduling (HAGS)",
                "Lets the GPU manage its own frame queue/VRAM instead of the driver. Requires a reboot.",
                RiskLevel.Safe, true,
                log => log(Sh.Reg("add \"HKLM\\SYSTEM\\CurrentControlSet\\Control\\GraphicsDrivers\" /v HwSchMode /t REG_DWORD /d 2 /f")),
                log => log(Sh.Reg("add \"HKLM\\SYSTEM\\CurrentControlSet\\Control\\GraphicsDrivers\" /v HwSchMode /t REG_DWORD /d 1 /f"))));

            l.Add(T(CAT_GAMING, "Disable Xbox Game Bar / Game DVR",
                "Kills the always-on background capture ring buffer. Real: fewer background hooks, better 1% lows in some games.",
                RiskLevel.Safe, true,
                log => {
                    log(Sh.Reg("add \"HKCU\\System\\GameConfigStore\" /v GameDVR_Enabled /t REG_DWORD /d 0 /f"));
                    log(Sh.Reg("add \"HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\GameDVR\" /v AllowGameDVR /t REG_DWORD /d 0 /f"));
                    log(Sh.Reg("add \"HKCU\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\GameDVR\" /v AppCaptureEnabled /t REG_DWORD /d 0 /f"));
                },
                log => {
                    log(Sh.Reg("add \"HKCU\\System\\GameConfigStore\" /v GameDVR_Enabled /t REG_DWORD /d 1 /f"));
                    log(Sh.Reg("add \"HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\GameDVR\" /v AllowGameDVR /t REG_DWORD /d 1 /f"));
                    log(Sh.Reg("add \"HKCU\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\GameDVR\" /v AppCaptureEnabled /t REG_DWORD /d 1 /f"));
                }));

            l.Add(T(CAT_GAMING, "Disable fullscreen optimizations (global default)",
                "Forces true exclusive fullscreen behavior by default. Can help FPS/latency in older DX9-11 titles; MODERATE: a few newer games/alt-tab flows prefer FSO on.",
                RiskLevel.Moderate, false,
                log => {
                    log(Sh.Reg("add \"HKCU\\System\\GameConfigStore\" /v GameDVR_FSEBehaviorMode /t REG_DWORD /d 2 /f"));
                    log(Sh.Reg("add \"HKCU\\System\\GameConfigStore\" /v GameDVR_HonorUserFSEBehaviorMode /t REG_DWORD /d 1 /f"));
                },
                log => {
                    log(Sh.Reg("delete \"HKCU\\System\\GameConfigStore\" /v GameDVR_FSEBehaviorMode /f"));
                    log(Sh.Reg("delete \"HKCU\\System\\GameConfigStore\" /v GameDVR_HonorUserFSEBehaviorMode /f"));
                }));

            l.Add(T(CAT_GAMING, "Boost multimedia scheduler priority for games",
                "Raises the 'Games' task category's CPU/GPU scheduling priority in the multimedia system profile.",
                RiskLevel.Safe, true,
                log => {
                    string k = "\"HKLM\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\Multimedia\\SystemProfile\\Tasks\\Games\"";
                    log(Sh.Reg("add " + k + " /v \"GPU Priority\" /t REG_DWORD /d 8 /f"));
                    log(Sh.Reg("add " + k + " /v \"Priority\" /t REG_DWORD /d 6 /f"));
                    log(Sh.Reg("add " + k + " /v \"Scheduling Category\" /t REG_SZ /d High /f"));
                    log(Sh.Reg("add " + k + " /v \"SFIO Priority\" /t REG_SZ /d High /f"));
                },
                log => {
                    string k = "\"HKLM\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\Multimedia\\SystemProfile\\Tasks\\Games\"";
                    log(Sh.Reg("add " + k + " /v \"GPU Priority\" /t REG_DWORD /d 8 /f"));
                    log(Sh.Reg("add " + k + " /v \"Priority\" /t REG_DWORD /d 2 /f"));
                    log(Sh.Reg("add " + k + " /v \"Scheduling Category\" /t REG_SZ /d Medium /f"));
                    log(Sh.Reg("add " + k + " /v \"SFIO Priority\" /t REG_SZ /d Normal /f"));
                }));

            l.Add(T(CAT_GAMING, "Increase GPU TDR delay",
                "Gives the GPU more time before Windows assumes it's hung and resets the driver. Helps avoid false crashes under heavy/overclocked load. MODERATE: masks a genuinely hung GPU for longer.",
                RiskLevel.Moderate, true,
                log => log(Sh.Reg("add \"HKLM\\SYSTEM\\CurrentControlSet\\Control\\GraphicsDrivers\" /v TdrDelay /t REG_DWORD /d 8 /f")),
                log => log(Sh.Reg("delete \"HKLM\\SYSTEM\\CurrentControlSet\\Control\\GraphicsDrivers\" /v TdrDelay /f"))));

            l.Add(T(CAT_GAMING, "SystemResponsiveness = 0 for games/multimedia",
                "Stops Windows reserving 20% CPU for background tasks during multimedia/gaming. NOTE: values of 0 can occasionally cause audio crackle under heavy load; 10 is a safer alternative.",
                RiskLevel.Moderate, true,
                log => log(Sh.Reg("add \"HKLM\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\Multimedia\\SystemProfile\" /v SystemResponsiveness /t REG_DWORD /d 0 /f")),
                log => log(Sh.Reg("add \"HKLM\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\Multimedia\\SystemProfile\" /v SystemResponsiveness /t REG_DWORD /d 20 /f"))));

            l.Add(T(CAT_GAMING, "Disable the Win+G Game Bar shortcut",
                "Stops the Xbox Game Bar overlay from popping up mid-game when Windows misreads a keypress as Win+G. Real annoyance fix; Game Bar can still be opened manually from the Start menu if you want it.",
                RiskLevel.Safe, true,
                log => log(Sh.Reg("add \"HKCU\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\GameDVR\" /v UseNexusForGameBarEnabled /t REG_DWORD /d 0 /f")),
                log => log(Sh.Reg("add \"HKCU\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\GameDVR\" /v UseNexusForGameBarEnabled /t REG_DWORD /d 1 /f"))));

            l.Add(T(CAT_GAMING, "Enable DirectX shader cache",
                "Lets DirectX cache compiled shaders to disk for faster subsequent game loads.",
                RiskLevel.Safe, false,
                log => log(Sh.Reg("add \"HKLM\\SOFTWARE\\Microsoft\\DirectX\" /v ShaderCache /t REG_DWORD /d 1 /f")),
                log => log(Sh.Reg("delete \"HKLM\\SOFTWARE\\Microsoft\\DirectX\" /v ShaderCache /f"))));
        }

        static void Startup(List<Tweak> l)
        {
            l.Add(T(CAT_STARTUP, "Disable background access for Store apps (global)",
                "Stops installed Store/UWP apps from running background tasks.",
                RiskLevel.Safe, true,
                log => log(Sh.Reg("add \"HKCU\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\BackgroundAccessApplications\" /v GlobalUserDisabled /t REG_DWORD /d 1 /f")),
                log => log(Sh.Reg("add \"HKCU\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\BackgroundAccessApplications\" /v GlobalUserDisabled /t REG_DWORD /d 0 /f"))));

            l.Add(T(CAT_STARTUP, "Disable Edge startup boost / background / prelaunch",
                "Stops Edge preloading itself at boot and running invisibly after you close it.",
                RiskLevel.Safe, true,
                log => { log(Sh.Reg("add \"HKLM\\SOFTWARE\\Policies\\Microsoft\\Edge\" /v StartupBoostEnabled /t REG_DWORD /d 0 /f")); log(Sh.Reg("add \"HKLM\\SOFTWARE\\Policies\\Microsoft\\Edge\" /v BackgroundModeEnabled /t REG_DWORD /d 0 /f")); log(Sh.Reg("add \"HKLM\\SOFTWARE\\Policies\\Microsoft\\MicrosoftEdge\\Main\" /v AllowPrelaunch /t REG_DWORD /d 0 /f")); },
                log => { log(Sh.Reg("delete \"HKLM\\SOFTWARE\\Policies\\Microsoft\\Edge\" /v StartupBoostEnabled /f")); log(Sh.Reg("delete \"HKLM\\SOFTWARE\\Policies\\Microsoft\\Edge\" /v BackgroundModeEnabled /f")); log(Sh.Reg("delete \"HKLM\\SOFTWARE\\Policies\\Microsoft\\MicrosoftEdge\\Main\" /v AllowPrelaunch /f")); }));

            l.Add(T(CAT_STARTUP, "Disable Microsoft Store background auto-updates",
                "MODERATE: apps installed from the Store won't silently update in the background; you'll need to update manually via the Store app.",
                RiskLevel.Moderate, false,
                log => log(Sh.Reg("add \"HKCU\\SOFTWARE\\Microsoft\\WindowsStore\\WindowsUpdate\" /v AutoDownload /t REG_DWORD /d 2 /f")),
                log => log(Sh.Reg("add \"HKCU\\SOFTWARE\\Microsoft\\WindowsStore\\WindowsUpdate\" /v AutoDownload /t REG_DWORD /d 4 /f"))));

            l.Add(T(CAT_STARTUP, "Reduce boot menu timeout to 5s",
                "Only matters if you dual-boot; harmless otherwise.",
                RiskLevel.Safe, false,
                log => log(Sh.RunCmd("bcdedit /timeout 5")),
                log => log(Sh.RunCmd("bcdedit /timeout 30"))));
        }

        static void Debloat(List<Tweak> l)
        {
            l.Add(T(CAT_DEBLOAT, "Remove default bloat apps (Solitaire, 3D Builder, Bing apps, king.com games, etc.)",
                "MODERATE / not one-click reversible: uninstalls a long list of preinstalled Microsoft/3rd-party UWP apps for all users. To get one back, reinstall it from the Microsoft Store.",
                RiskLevel.Moderate, false,
                log => log(Sh.RunPS(
                    "$apps = 'Microsoft.3DBuilder','Microsoft.BingFinance','Microsoft.BingNews','Microsoft.BingSports','Microsoft.BingWeather','Microsoft.BingTranslator','Microsoft.GetHelp','Microsoft.Getstarted','Microsoft.Messaging','Microsoft.Microsoft3DViewer','Microsoft.MicrosoftOfficeHub','Microsoft.MicrosoftSolitaireCollection','Microsoft.MixedReality.Portal','Microsoft.Office.OneNote','Microsoft.People','Microsoft.Print3D','Microsoft.SkypeApp','Microsoft.Wallet','Microsoft.WindowsAlarms','Microsoft.WindowsCamera','Microsoft.windowscommunicationsapps','Microsoft.WindowsFeedbackHub','Microsoft.WindowsMaps','Microsoft.WindowsSoundRecorder','Microsoft.YourPhone','Microsoft.ZuneMusic','Microsoft.ZuneVideo','Microsoft.Todos','Clipchamp.Clipchamp','Microsoft.MicrosoftStickyNotes','Microsoft.Whiteboard','Microsoft.NetworkSpeedTest','Microsoft.Paint3D','king.com.CandyCrushSaga','king.com.CandyCrushSodaSaga','king.com.BubbleWitch3Saga','king.com.FarmHeroesSaga','Facebook.Facebook','SpotifyAB.SpotifyMusic'\r\n" +
                    "foreach ($a in $apps) { Get-AppxPackage -AllUsers \"*$a*\" | Remove-AppxPackage -EA SilentlyContinue; Get-AppxProvisionedPackage -Online | Where-Object DisplayName -like \"*$a*\" | Remove-AppxProvisionedPackage -Online -EA SilentlyContinue }\r\n" +
                    "'done'")),
                log => log("Not automatically reversible. Reinstall removed apps from the Microsoft Store if you want them back.")));

            l.Add(T(CAT_DEBLOAT, "Disable Cortana",
                "Turns off the (long-deprecated on Win10 22H2) Cortana assistant integration.",
                RiskLevel.Safe, true,
                log => { log(Sh.Reg("add \"HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\Windows Search\" /v AllowCortana /t REG_DWORD /d 0 /f")); log(Sh.Reg("add \"HKCU\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Search\" /v CortanaConsent /t REG_DWORD /d 0 /f")); },
                log => { log(Sh.Reg("delete \"HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\Windows Search\" /v AllowCortana /f")); log(Sh.Reg("delete \"HKCU\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Search\" /v CortanaConsent /f")); }));

            l.Add(T(CAT_DEBLOAT, "Remove OneDrive from autostart",
                "OneDrive app stays installed, just stops launching at login. Reversible from Settings > Apps > Startup.",
                RiskLevel.Safe, true,
                log => { log(Sh.Reg("delete \"HKCU\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run\" /v OneDrive /f")); log(Sh.Reg("add \"HKCU\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Explorer\\StartupApproved\\Run\" /v OneDrive /t REG_BINARY /d 0300000000000000 /f")); },
                log => log(Sh.Reg("add \"HKCU\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Explorer\\StartupApproved\\Run\" /v OneDrive /t REG_BINARY /d 0200000000000000 /f"))));

            l.Add(T(CAT_DEBLOAT, "Disable Copilot && Recall AI features",
                "Privacy-positive; on Windows 10 22H2 these features aren't present yet, so this is a harmless no-op / future-proofing.",
                RiskLevel.Safe, true,
                log => {
                    log(Sh.Reg("add \"HKCU\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced\" /v ShowCopilotButton /t REG_DWORD /d 0 /f"));
                    log(Sh.Reg("add \"HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\WindowsCopilot\" /v TurnOffWindowsCopilot /t REG_DWORD /d 1 /f"));
                    log(Sh.Reg("add \"HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\WindowsAI\" /v DisableAIDataAnalysis /t REG_DWORD /d 1 /f"));
                },
                log => {
                    log(Sh.Reg("add \"HKCU\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced\" /v ShowCopilotButton /t REG_DWORD /d 1 /f"));
                    log(Sh.Reg("delete \"HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\WindowsCopilot\" /v TurnOffWindowsCopilot /f"));
                    log(Sh.Reg("delete \"HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\WindowsAI\" /v DisableAIDataAnalysis /f"));
                }));

            l.Add(T(CAT_DEBLOAT, "Remove Start Menu ads / suggestions / Spotlight",
                "Turns off 'suggested apps', silent installed apps and lock-screen ad content.",
                RiskLevel.Safe, true,
                log => {
                    string k = "\"HKCU\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\ContentDeliveryManager\"";
                    foreach (var v in new[] { "SilentInstalledAppsEnabled", "SystemPaneSuggestionsEnabled", "PreInstalledAppsEnabled", "OemPreInstalledAppsEnabled", "SoftLandingEnabled", "SubscribedContentEnabled", "RotatingLockScreenEnabled" })
                        log(Sh.Reg("add " + k + " /v " + v + " /t REG_DWORD /d 0 /f"));
                },
                log => {
                    string k = "\"HKCU\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\ContentDeliveryManager\"";
                    foreach (var v in new[] { "SilentInstalledAppsEnabled", "SystemPaneSuggestionsEnabled", "PreInstalledAppsEnabled", "OemPreInstalledAppsEnabled", "SoftLandingEnabled", "SubscribedContentEnabled", "RotatingLockScreenEnabled" })
                        log(Sh.Reg("add " + k + " /v " + v + " /t REG_DWORD /d 1 /f"));
                }));

            l.Add(T(CAT_DEBLOAT, "Disable Start Menu Bing web search",
                "Start-menu search stays local-only, no web results injected.",
                RiskLevel.Safe, true,
                log => { log(Sh.Reg("add \"HKCU\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Search\" /v BingSearchEnabled /t REG_DWORD /d 0 /f")); log(Sh.Reg("add \"HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\Windows Search\" /v DisableWebSearch /t REG_DWORD /d 1 /f")); },
                log => { log(Sh.Reg("add \"HKCU\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Search\" /v BingSearchEnabled /t REG_DWORD /d 1 /f")); log(Sh.Reg("delete \"HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\Windows Search\" /v DisableWebSearch /f")); }));

            l.Add(T(CAT_DEBLOAT, "File Explorer: show extensions/hidden files, open to This PC",
                "Preference: shows file extensions and hidden system files, and makes Explorer open to 'This PC' instead of 'Quick Access'.",
                RiskLevel.Safe, false,
                log => {
                    log(Sh.Reg("add \"HKCU\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced\" /v LaunchTo /t REG_DWORD /d 1 /f"));
                    log(Sh.Reg("add \"HKCU\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced\" /v HideFileExt /t REG_DWORD /d 0 /f"));
                    log(Sh.Reg("add \"HKCU\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced\" /v ShowSuperHidden /t REG_DWORD /d 1 /f"));
                },
                log => {
                    log(Sh.Reg("add \"HKCU\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced\" /v LaunchTo /t REG_DWORD /d 2 /f"));
                    log(Sh.Reg("add \"HKCU\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced\" /v HideFileExt /t REG_DWORD /d 1 /f"));
                    log(Sh.Reg("add \"HKCU\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced\" /v ShowSuperHidden /t REG_DWORD /d 0 /f"));
                }));

            l.Add(T(CAT_DEBLOAT, "Disable Reserved Storage",
                "Frees several GB previously reserved by Windows for its own updates.",
                RiskLevel.Safe, false,
                log => log(Sh.Reg("add \"HKLM\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\ReserveManager\" /v ShippedWithReserves /t REG_DWORD /d 0 /f")),
                log => log(Sh.Reg("add \"HKLM\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\ReserveManager\" /v ShippedWithReserves /t REG_DWORD /d 1 /f"))));

            l.Add(T(CAT_DEBLOAT, "Windows Update: set Active Hours 8am-11pm",
                "Tells Windows you're actively using the PC 8am-11pm, so it won't auto-restart for updates in the middle of a gaming session during that window. Real and safe — purely a scheduling hint.",
                RiskLevel.Safe, true,
                log => { log(Sh.Reg("add \"HKLM\\SOFTWARE\\Microsoft\\WindowsUpdate\\UX\\Settings\" /v ActiveHoursStart /t REG_DWORD /d 8 /f")); log(Sh.Reg("add \"HKLM\\SOFTWARE\\Microsoft\\WindowsUpdate\\UX\\Settings\" /v ActiveHoursEnd /t REG_DWORD /d 23 /f")); log(Sh.Reg("add \"HKLM\\SOFTWARE\\Microsoft\\WindowsUpdate\\UX\\Settings\" /v IsActiveHoursEnabled /t REG_DWORD /d 1 /f")); },
                log => { log(Sh.Reg("add \"HKLM\\SOFTWARE\\Microsoft\\WindowsUpdate\\UX\\Settings\" /v ActiveHoursStart /t REG_DWORD /d 8 /f")); log(Sh.Reg("add \"HKLM\\SOFTWARE\\Microsoft\\WindowsUpdate\\UX\\Settings\" /v ActiveHoursEnd /t REG_DWORD /d 17 /f")); }));

            l.Add(T(CAT_DEBLOAT, "Stop Windows Update from auto-installing driver updates",
                "MODERATE, real fix for a common gamer pain point: Windows Update can silently replace a newer NVIDIA/AMD/Intel driver you installed manually with an older WHQL one it prefers. This stops that for ALL hardware, not just GPU — you'll need to update chipset/audio/etc. drivers manually too.",
                RiskLevel.Moderate, false,
                log => {
                    log(Sh.Reg("add \"HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\Device Metadata\" /v PreventDeviceMetadataFromNetwork /t REG_DWORD /d 1 /f"));
                    log(Sh.Reg("add \"HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\DriverSearching\" /v DontSearchWindowsUpdate /t REG_DWORD /d 1 /f"));
                    log(Sh.Reg("add \"HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\DriverSearching\" /v DontPromptForWindowsUpdate /t REG_DWORD /d 1 /f"));
                    log(Sh.Reg("add \"HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\WindowsUpdate\" /v ExcludeWUDriversInQualityUpdate /t REG_DWORD /d 1 /f"));
                },
                log => {
                    log(Sh.Reg("delete \"HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\Device Metadata\" /v PreventDeviceMetadataFromNetwork /f"));
                    log(Sh.Reg("delete \"HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\DriverSearching\" /v DontSearchWindowsUpdate /f"));
                    log(Sh.Reg("delete \"HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\DriverSearching\" /v DontPromptForWindowsUpdate /f"));
                    log(Sh.Reg("delete \"HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\WindowsUpdate\" /v ExcludeWUDriversInQualityUpdate /f"));
                }));

            l.Add(T(CAT_DEBLOAT, "Uninstall OneDrive completely",
                "ADVANCED: fully uninstalls the OneDrive client (not just autostart). Your files stay on disk. To bring it back later, run '%SystemRoot%\\SysWOW64\\OneDriveSetup.exe' or reinstall from microsoft.com/onedrive.",
                RiskLevel.Advanced, false,
                log => {
                    log(Sh.RunCmd("taskkill /f /im OneDrive.exe"));
                    log(Sh.RunCmd("%SystemRoot%\\SysWOW64\\OneDriveSetup.exe /uninstall"));
                },
                log => log(Sh.RunCmd("%SystemRoot%\\SysWOW64\\OneDriveSetup.exe"))));

            l.Add(T(CAT_DEBLOAT, "Hide '3D Objects' from This PC",
                "Cosmetic: removes the mostly-unused 3D Objects folder shortcut from File Explorer's This PC view.",
                RiskLevel.Safe, false,
                log => {
                    log(Sh.Reg("delete \"HKLM\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Explorer\\MyComputer\\NameSpace\\{0DB7E03F-FC29-4DC6-9020-FF41B59E513A}\" /f"));
                    log(Sh.Reg("delete \"HKLM\\SOFTWARE\\WOW6432Node\\Microsoft\\Windows\\CurrentVersion\\Explorer\\MyComputer\\NameSpace\\{0DB7E03F-FC29-4DC6-9020-FF41B59E513A}\" /f"));
                },
                log => {
                    log(Sh.Reg("add \"HKLM\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Explorer\\MyComputer\\NameSpace\\{0DB7E03F-FC29-4DC6-9020-FF41B59E513A}\" /f"));
                    log(Sh.Reg("add \"HKLM\\SOFTWARE\\WOW6432Node\\Microsoft\\Windows\\CurrentVersion\\Explorer\\MyComputer\\NameSpace\\{0DB7E03F-FC29-4DC6-9020-FF41B59E513A}\" /f"));
                }));

            l.Add(T(CAT_DEBLOAT, "Disable recent/frequent items in Quick Access",
                "Preference: Quick Access shows only your pinned folders, not an auto-tracked history of recently/frequently used files and folders.",
                RiskLevel.Safe, false,
                log => { log(Sh.Reg("add \"HKCU\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced\" /v ShowFrequent /t REG_DWORD /d 0 /f")); log(Sh.Reg("add \"HKCU\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced\" /v ShowRecent /t REG_DWORD /d 0 /f")); },
                log => { log(Sh.Reg("add \"HKCU\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced\" /v ShowFrequent /t REG_DWORD /d 1 /f")); log(Sh.Reg("add \"HKCU\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced\" /v ShowRecent /t REG_DWORD /d 1 /f")); }));

            l.Add(T(CAT_DEBLOAT, "Always show all system tray icons",
                "Preference: stops Windows from auto-hiding tray icons behind the ^ overflow arrow.",
                RiskLevel.Safe, false,
                log => log(Sh.Reg("add \"HKCU\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Explorer\" /v EnableAutoTray /t REG_DWORD /d 0 /f")),
                log => log(Sh.Reg("add \"HKCU\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Explorer\" /v EnableAutoTray /t REG_DWORD /d 1 /f"))));
        }

        static void ServicesSafe(List<Tweak> l)
        {
            l.Add(Svc(CAT_SVC, "Connected User Experiences and Telemetry", "DiagTrack", "Main telemetry upload service.", RiskLevel.Safe, true, "auto"));
            l.Add(Svc(CAT_SVC, "WAP Push Message Routing", "dmwappushservice", "Telemetry-adjacent push routing.", RiskLevel.Safe, true, "demand"));
            l.Add(Svc(CAT_SVC, "Diagnostics Hub Standard Collector", "diagnosticshub.standardcollector.service", "Performance diagnostics collector, mostly used by Visual Studio.", RiskLevel.Safe, true, "demand"));
            l.Add(Svc(CAT_SVC, "Diagnostic Execution Service", "diagsvc", "Background diagnostics.", RiskLevel.Safe, true, "demand"));
            l.Add(Svc(CAT_SVC, "Windows Error Reporting", "WerSvc", "Uploads crash reports to Microsoft.", RiskLevel.Safe, true, "demand"));
            l.Add(Svc(CAT_SVC, "Superfetch / SysMain", "SysMain", "Pre-loads frequently used apps into RAM. Small benefit on NVMe+32GB; disabling slightly slows cold app launches.", RiskLevel.Moderate, true, "delayed-auto"));
            l.Add(Svc(CAT_SVC, "Windows Search indexing", "WSearch", "Powers fast Start-menu/Explorer search. Disabling makes searches slower (full scan).", RiskLevel.Moderate, false, "delayed-auto"));
            l.Add(Svc(CAT_SVC, "Downloaded Maps Manager", "MapsBroker", "Only needed for the offline Maps app.", RiskLevel.Safe, true, "demand"));
            l.Add(Svc(CAT_SVC, "Geolocation Service", "lfsvc", "Only needed for location-aware apps.", RiskLevel.Safe, true, "demand"));
            l.Add(Svc(CAT_SVC, "Retail Demo Service", "RetailDemo", "Store-display demo mode; irrelevant for a personal PC.", RiskLevel.Safe, true, "demand"));
            l.Add(Svc(CAT_SVC, "Parental Controls", "WpcMonSvc", "Only needed if Family Safety parental controls are used.", RiskLevel.Safe, true, "demand"));
            l.Add(Svc(CAT_SVC, "Fax", "Fax", "Fax support. Nobody needs this in 2026.", RiskLevel.Safe, true, "demand"));
            l.Add(Svc(CAT_SVC, "Telephony API", "TapiSrv", "Legacy modem/telephony support.", RiskLevel.Safe, true, "demand"));
            l.Add(Svc(CAT_SVC, "Phone Service", "PhoneSvc", "Telephony state; only relevant with Phone Link.", RiskLevel.Safe, true, "demand"));
            l.Add(Svc(CAT_SVC, "Wallet Service", "WalletService", "Windows Wallet, essentially unused.", RiskLevel.Safe, true, "demand"));
            l.Add(Svc(CAT_SVC, "Connected Devices Platform", "CDPSvc", "Powers Nearby Sharing / phone linking features.", RiskLevel.Moderate, true, "demand"));
            l.Add(Svc(CAT_SVC, "Sync Host (mail/calendar background sync)", "OneSyncSvc", "Background sync for Mail/Calendar/People apps.", RiskLevel.Moderate, true, "demand"));
            l.Add(Svc(CAT_SVC, "Delivery Optimization", "DoSvc", "Peer-to-peer Windows/Store update sharing. Normal downloads still work.", RiskLevel.Moderate, true, "demand"));
            l.Add(Svc(CAT_SVC, "Push Notifications", "WpnService", "Toast notification delivery for background apps. Disabling can silence some app notifications.", RiskLevel.Moderate, false, "demand"));
            l.Add(Svc(CAT_SVC, "Microsoft iSCSI Initiator", "MSiSCSI", "Only needed for iSCSI network storage.", RiskLevel.Safe, true, "demand"));
            l.Add(Svc(CAT_SVC, "Smart Card", "SCardSvr", "Only needed for smart-card readers.", RiskLevel.Safe, true, "demand"));
            l.Add(Svc(CAT_SVC, "Smart Card Removal Policy", "SCPolicySvc", "Only needed for smart-card readers.", RiskLevel.Safe, true, "demand"));
            l.Add(Svc(CAT_SVC, "Mobile Hotspot", "icssvc", "Only needed if you share your PC's internet as a hotspot.", RiskLevel.Safe, true, "demand"));
            l.Add(Svc(CAT_SVC, "Software Protection sub-service", "SEMgrSvc", "Payments & NFC/SE Manager, essentially unused.", RiskLevel.Safe, true, "demand"));
            l.Add(Svc(CAT_SVC, "Offline Files", "CscService", "Only needed for corporate offline-file sync.", RiskLevel.Safe, true, "demand"));
            l.Add(Svc(CAT_SVC, "WebDAV Client Redirector", "WebClient", "Only needed for WebDAV network shares.", RiskLevel.Safe, true, "demand"));
            l.Add(Svc(CAT_SVC, "Mixed Reality OpenXR", "MixedRealityOpenXRSvc", "Only needed for VR/mixed-reality headsets.", RiskLevel.Safe, true, "demand"));
            l.Add(Svc(CAT_SVC, "Distributed Link Tracking Client", "TrkWks", "Tracks shortcuts across NTFS volumes; irrelevant for a single-drive gaming PC.", RiskLevel.Safe, true, "demand"));
            l.Add(Svc(CAT_SVC, "Clipboard User Service", "cbdhsvc", "Powers cloud clipboard sync only; local Win+V history still works.", RiskLevel.Safe, false, "demand"));
            l.Add(Svc(CAT_SVC, "Edge auto-update", "edgeupdate", "Edge won't auto-update in the background (it still runs fine).", RiskLevel.Moderate, true, "demand"));
        }

        static void ServicesAdvanced(List<Tweak> l)
        {
            l.Add(Svc(CAT_SVC_ADV, "Bluetooth Audio Gateway", "BTAGService", "BREAKS Bluetooth headset/audio profiles.", RiskLevel.Advanced, false, "demand"));
            l.Add(Svc(CAT_SVC_ADV, "Bluetooth Support Service", "bthserv", "BREAKS all Bluetooth devices (mice, controllers, headsets).", RiskLevel.Advanced, false, "demand"));
            l.Add(Svc(CAT_SVC_ADV, "Xbox Live Auth Manager", "XblAuthManager", "BREAKS Xbox/Game Pass sign-in.", RiskLevel.Advanced, false, "demand"));
            l.Add(Svc(CAT_SVC_ADV, "Xbox Live Game Save", "XblGameSave", "BREAKS cloud game-save sync for Xbox/Game Pass titles.", RiskLevel.Advanced, false, "demand"));
            l.Add(Svc(CAT_SVC_ADV, "Xbox Live Networking", "XboxNetApiSvc", "BREAKS Xbox multiplayer networking features.", RiskLevel.Advanced, false, "demand"));
            l.Add(Svc(CAT_SVC_ADV, "Xbox Accessory Management", "XboxGipSvc", "BREAKS Xbox controllers (wired and Bluetooth).", RiskLevel.Advanced, false, "demand"));
            l.Add(Svc(CAT_SVC_ADV, "Remote Registry", "RemoteRegistry", "Allows remote registry access. Disabled by default on most systems already; safe security hardening.", RiskLevel.Safe, true, "demand"));
            l.Add(Svc(CAT_SVC_ADV, "Remote Access Connection Manager", "RasMan", "BREAKS VPN and dial-up connections.", RiskLevel.Advanced, false, "demand"));
            l.Add(Svc(CAT_SVC_ADV, "Secondary Logon", "seclogon", "BREAKS 'Run as different user' and some installers.", RiskLevel.Advanced, false, "demand"));
            l.Add(Svc(CAT_SVC_ADV, "OpenSSH Authentication Agent", "ssh-agent", "BREAKS SSH key agent (matters if you use Git/SSH keys).", RiskLevel.Advanced, false, "demand"));
            l.Add(Svc(CAT_SVC_ADV, "Print Notifications", "PrintNotify", "BREAKS printer notifications/driver installs.", RiskLevel.Advanced, false, "demand"));
            l.Add(Svc(CAT_SVC_ADV, "Print Spooler", "Spooler", "BREAKS all printing.", RiskLevel.Advanced, false, "auto"));
            l.Add(Svc(CAT_SVC_ADV, "Remote Desktop Services", "TermService", "BREAKS incoming Remote Desktop connections to this PC.", RiskLevel.Advanced, false, "demand"));
            l.Add(Svc(CAT_SVC_ADV, "Remote Desktop Configuration", "SessionEnv", "BREAKS Remote Desktop configuration.", RiskLevel.Advanced, false, "demand"));
            l.Add(Svc(CAT_SVC_ADV, "Windows Update (set to Manual)", "wuauserv", "Windows Update won't run automatically; you must trigger checks manually. You lose automatic security patches.", RiskLevel.Advanced, false, "demand"));
            l.Add(Svc(CAT_SVC_ADV, "Background Intelligent Transfer Service", "BITS", "BREAKS Windows Update downloads and many app updaters/installers that rely on BITS.", RiskLevel.Advanced, false, "demand"));
            l.Add(Svc(CAT_SVC_ADV, "IP Helper", "iphlpsvc", "BREAKS IPv6 tunneling, Teredo, and some VPN clients.", RiskLevel.Advanced, false, "auto"));
            l.Add(Svc(CAT_SVC_ADV, "Windows Image Acquisition", "stisvc", "BREAKS scanners and some webcam software that uses WIA.", RiskLevel.Moderate, false, "demand"));
            l.Add(Svc(CAT_SVC_ADV, "Windows Biometric Service", "WbioSrvc", "BREAKS fingerprint/face (Windows Hello) sign-in.", RiskLevel.Advanced, false, "demand"));
            l.Add(Svc(CAT_SVC_ADV, "BitLocker Drive Encryption Service", "BDESVC", "BREAKS BitLocker management. Do NOT disable if you use BitLocker.", RiskLevel.Advanced, false, "demand"));
            l.Add(Svc(CAT_SVC_ADV, "Encrypting File System (EFS)", "EFS", "BREAKS access to any EFS-encrypted files.", RiskLevel.Advanced, false, "demand"));
            l.Add(Svc(CAT_SVC_ADV, "Portable Device Enumerator", "WPDBusEnum", "BREAKS phone/camera USB media transfer (MTP).", RiskLevel.Advanced, false, "demand"));
            l.Add(Svc(CAT_SVC_ADV, "SSDP Discovery / UPnP", "SSDPSRV", "BREAKS smart-TV casting, DLNA and UPnP device discovery.", RiskLevel.Moderate, false, "demand"));
            l.Add(Svc(CAT_SVC_ADV, "Function Discovery Provider Host", "fdPHost", "BREAKS network device discovery (finding printers/NAS on the network).", RiskLevel.Moderate, false, "demand"));
            l.Add(Svc(CAT_SVC_ADV, "Windows Time", "W32Time", "Causes your system clock to drift over time (breaks online-game anti-cheat time checks, certificate validation).", RiskLevel.Advanced, false, "auto"));
            l.Add(Svc(CAT_SVC_ADV, "User Data Access / Token Broker", "TokenBroker", "BREAKS Microsoft account sign-in for some Store apps.", RiskLevel.Advanced, false, "demand"));
            l.Add(Svc(CAT_SVC_ADV, "Microsoft Account Sign-in Assistant", "wlidsvc", "BREAKS Microsoft account sign-in system-wide (Store, Xbox, OneDrive).", RiskLevel.Advanced, false, "demand"));
        }

        static void Tasks(List<Tweak> l)
        {
            string[] telemetryTasks = {
                "\\Microsoft\\Windows\\Application Experience\\Microsoft Compatibility Appraiser",
                "\\Microsoft\\Windows\\Application Experience\\ProgramDataUpdater",
                "\\Microsoft\\Windows\\Application Experience\\StartupAppTask",
                "\\Microsoft\\Windows\\Customer Experience Improvement Program\\Consolidator",
                "\\Microsoft\\Windows\\Customer Experience Improvement Program\\UsbCeip",
                "\\Microsoft\\Windows\\Customer Experience Improvement Program\\KernelCeipTask",
                "\\Microsoft\\Windows\\Autochk\\Proxy",
                "\\Microsoft\\Windows\\DiskDiagnostic\\Microsoft-Windows-DiskDiagnosticDataCollector",
                "\\Microsoft\\Windows\\Feedback\\Siuf\\DmClient",
                "\\Microsoft\\Windows\\Feedback\\Siuf\\DmClientOnScenarioDownload",
                "\\Microsoft\\Windows\\Windows Error Reporting\\QueueReporting",
                "\\Microsoft\\Windows\\NetTrace\\GatherNetworkInfo",
                "\\Microsoft\\Windows\\Maps\\MapsUpdateTask",
                "\\Microsoft\\Windows\\Maps\\MapsToastTask",
                "\\Microsoft\\Windows\\Speech\\SpeechModelDownloadTask",
                "\\Microsoft\\Windows\\MUI\\LPRemove"
            };
            l.Add(T(CAT_TASKS, "Disable telemetry/CEIP/diagnostic scheduled tasks (16 tasks)",
                "Compatibility Appraiser especially is a known periodic CPU/disk spike (CompatTelRunner.exe). All uniformly low-risk to disable.",
                RiskLevel.Safe, true,
                log => { foreach (var t in telemetryTasks) log(Sh.Task(t, false)); },
                log => { foreach (var t in telemetryTasks) log(Sh.Task(t, true)); }));

            string[] updateTasks = {
                "\\Microsoft\\Windows\\UpdateOrchestrator\\Report policies",
                "\\Microsoft\\Windows\\UpdateOrchestrator\\USO_UxBroker",
                "\\Microsoft\\Windows\\UpdateOrchestrator\\Schedule Scan"
            };
            l.Add(T(CAT_TASKS, "Disable Windows Update background orchestrator tasks",
                "MODERATE: Windows Update may check for/install updates less proactively in the background. You can still update manually via Settings.",
                RiskLevel.Moderate, false,
                log => { foreach (var t in updateTasks) log(Sh.Task(t, false)); },
                log => { foreach (var t in updateTasks) log(Sh.Task(t, true)); }));

            l.Add(T(CAT_TASKS, "Disable scheduled Defrag / maintenance tasks",
                "Turns off automatic disk defrag/optimization and the general Windows maintenance task. Fine on an SSD/NVMe (TRIM still works separately); run Optimize Drives manually if needed.",
                RiskLevel.Safe, false,
                log => { log(Sh.Task("\\Microsoft\\Windows\\Defrag\\ScheduledDefrag", false)); log(Sh.Task("\\Microsoft\\Windows\\TaskScheduler\\Regular Maintenance", false)); },
                log => { log(Sh.Task("\\Microsoft\\Windows\\Defrag\\ScheduledDefrag", true)); log(Sh.Task("\\Microsoft\\Windows\\TaskScheduler\\Regular Maintenance", true)); }));
        }

        static void Storage(List<Tweak> l)
        {
            l.Add(T(CAT_STORAGE, "Disable NTFS last-access timestamp updates",
                "Skips writing a timestamp every time a file is merely read. Small, real reduction in disk write chatter.",
                RiskLevel.Safe, true,
                log => log(Sh.RunCmd("fsutil behavior set disablelastaccess 1")),
                log => log(Sh.RunCmd("fsutil behavior set disablelastaccess 0"))));

            l.Add(T(CAT_STORAGE, "Disable 8.3 short filename generation",
                "Removes legacy DOS-style short filenames on new files. Very low risk; only matters to decades-old software.",
                RiskLevel.Safe, true,
                log => log(Sh.RunCmd("fsutil behavior set disable8dot3 1")),
                log => log(Sh.RunCmd("fsutil behavior set disable8dot3 0"))));

            l.Add(T(CAT_STORAGE, "Pagefile: set to System-Managed (recommended)",
                "Lets Windows size the pagefile automatically based on installed RAM. Safer than a small fixed cap on a 32GB+ system.",
                RiskLevel.Safe, true,
                log => log(Sh.RunPS("$w = Get-WmiObject Win32_ComputerSystem; $w.AutomaticManagedPagefile = $true; $w.Put() | Out-Null; 'set to system-managed, reboot to apply'")),
                log => log(Sh.RunPS("$w = Get-WmiObject Win32_ComputerSystem; $w.AutomaticManagedPagefile = $false; $w.Put() | Out-Null; $pf = Get-WmiObject Win32_PageFileSetting; if ($pf) { $pf.InitialSize=4096; $pf.MaximumSize=8192; $pf.Put() | Out-Null }"))));

            l.Add(T(CAT_STORAGE, "One-click cleanup (temp, prefetch, thumbnail cache, recycle bin, WU cache, CBS logs)",
                "Deletes only well-known safe-to-delete cache/temp locations. Not reversible (they're caches, not data), but nothing important is lost.",
                RiskLevel.Safe, true,
                log => {
                    log(Sh.RunCmd("del /q /f /s \"%TEMP%\\*\" & del /q /f /s \"C:\\Windows\\Temp\\*\""));
                    log(Sh.RunCmd("del /q /f /s \"C:\\Windows\\Prefetch\\*\""));
                    log(Sh.RunCmd("del /q /f /s \"%LOCALAPPDATA%\\Microsoft\\Windows\\Explorer\\thumbcache_*.db\""));
                    log(Sh.RunCmd("net stop wuauserv & del /q /f /s \"C:\\Windows\\SoftwareDistribution\\Download\\*\" & net start wuauserv"));
                    log(Sh.RunCmd("del /q /f /s \"C:\\Windows\\Logs\\CBS\\*\""));
                    log(Sh.RunPS("Clear-RecycleBin -Force -EA SilentlyContinue"));
                },
                log => log("Cache cleanup has nothing to revert (caches regenerate automatically).")));

            l.Add(T(CAT_STORAGE, "Remove Windows.old",
                "ADVANCED: permanently removes the previous-Windows-version backup folder. You lose the ability to roll back a Windows upgrade after this.",
                RiskLevel.Advanced, false,
                log => log(Sh.RunPS("if (Test-Path 'C:\\Windows.old') { Remove-Item -Path 'C:\\Windows.old' -Recurse -Force -EA SilentlyContinue; 'removed' } else { 'not present' }")),
                log => log("Not reversible — Windows.old cannot be restored once deleted.")));

            l.Add(T(CAT_STORAGE, "SysMain light-touch: prefetch on, boot supercache off",
                "MODERATE alternative to fully disabling the SysMain service: keeps regular app prefetching (faster repeat launches) but turns off the boot/app 'supercaching' behavior that does most of SysMain's background disk reading. Only takes effect if the SysMain service itself is still running — don't combine with the Services-tab SysMain disable.",
                RiskLevel.Moderate, false,
                log => {
                    string k = "\"HKLM\\SYSTEM\\CurrentControlSet\\Control\\Session Manager\\Memory Management\\PrefetchParameters\"";
                    log(Sh.Reg("add " + k + " /v EnablePrefetcher /t REG_DWORD /d 3 /f"));
                    log(Sh.Reg("add " + k + " /v EnableSuperfetch /t REG_DWORD /d 0 /f"));
                },
                log => {
                    string k = "\"HKLM\\SYSTEM\\CurrentControlSet\\Control\\Session Manager\\Memory Management\\PrefetchParameters\"";
                    log(Sh.Reg("add " + k + " /v EnablePrefetcher /t REG_DWORD /d 3 /f"));
                    log(Sh.Reg("add " + k + " /v EnableSuperfetch /t REG_DWORD /d 3 /f"));
                }));

            l.Add(T(CAT_STORAGE, "Increase Explorer icon cache size",
                "Raises the max cached icon count so folders with lots of unique file icons redraw/scroll without re-rendering icons from disk each time. Marginal but real on icon-heavy folders.",
                RiskLevel.Safe, false,
                log => log(Sh.Reg("add \"HKCU\\Software\\Classes\\Local Settings\\Software\\Microsoft\\Windows\\Shell\\Bags\\Icons\" /v Max Cached Icons /t REG_SZ /d 4096 /f")),
                log => log(Sh.Reg("delete \"HKCU\\Software\\Classes\\Local Settings\\Software\\Microsoft\\Windows\\Shell\\Bags\\Icons\" /v Max Cached Icons /f"))));

            l.Add(T(CAT_STORAGE, "Run SSD TRIM pass",
                "Runs a TRIM/retrim pass on C: to keep the SSD's free-space map healthy.",
                RiskLevel.Safe, true,
                log => log(Sh.RunCmd("defrag C: /L")),
                log => log("Nothing to revert — TRIM is a maintenance operation.")));
        }

        static void RamBoot(List<Tweak> l)
        {
            l.Add(T(CAT_RAM, "Foreground/background priority separation (Win32PrioritySeparation)",
                "Standard gaming tweak: gives the foreground app a bigger, variable CPU time-slice boost over background apps.",
                RiskLevel.Safe, true,
                log => log(Sh.Reg("add \"HKLM\\SYSTEM\\CurrentControlSet\\Control\\PriorityControl\" /v Win32PrioritySeparation /t REG_DWORD /d 38 /f")),
                log => log(Sh.Reg("add \"HKLM\\SYSTEM\\CurrentControlSet\\Control\\PriorityControl\" /v Win32PrioritySeparation /t REG_DWORD /d 2 /f"))));

            l.Add(T(CAT_RAM, "Keep kernel in physical RAM (disable paging executive)",
                "Prevents the kernel from ever being paged to disk. Uses a little more RAM; irrelevant with 32GB.",
                RiskLevel.Safe, true,
                log => log(Sh.Reg("add \"HKLM\\SYSTEM\\CurrentControlSet\\Control\\Session Manager\\Memory Management\" /v DisablePagingExecutive /t REG_DWORD /d 1 /f")),
                log => log(Sh.Reg("add \"HKLM\\SYSTEM\\CurrentControlSet\\Control\\Session Manager\\Memory Management\" /v DisablePagingExecutive /t REG_DWORD /d 0 /f"))));

            l.Add(T(CAT_RAM, "Svchost split threshold to installed RAM",
                "Lets Windows split more services into separate svchost.exe processes based on how much RAM you actually have (legitimate, well-known safe tweak).",
                RiskLevel.Safe, true,
                log => log(Sh.RunPS("$ramKB = (Get-CimInstance Win32_ComputerSystem).TotalPhysicalMemory/1KB; reg add 'HKLM\\SYSTEM\\CurrentControlSet\\Control' /v SvcHostSplitThresholdInKB /t REG_DWORD /d ([int]$ramKB) /f")),
                log => log(Sh.Reg("add \"HKLM\\SYSTEM\\CurrentControlSet\\Control\" /v SvcHostSplitThresholdInKB /t REG_DWORD /d 3670016 /f"))));

            l.Add(T(CAT_RAM, "Reduce hung-app / kill timeouts",
                "MODERATE: Windows force-closes unresponsive apps/services faster. Can occasionally kill an app that was just slow (e.g. mid-save), not actually hung.",
                RiskLevel.Moderate, false,
                log => { log(Sh.Reg("add \"HKCU\\Control Panel\\Desktop\" /v WaitToKillAppTimeout /t REG_SZ /d 2000 /f")); log(Sh.Reg("add \"HKCU\\Control Panel\\Desktop\" /v HungAppTimeout /t REG_SZ /d 1000 /f")); log(Sh.Reg("add \"HKLM\\SYSTEM\\CurrentControlSet\\Control\" /v WaitToKillServiceTimeout /t REG_SZ /d 2000 /f")); },
                log => { log(Sh.Reg("add \"HKCU\\Control Panel\\Desktop\" /v WaitToKillAppTimeout /t REG_SZ /d 5000 /f")); log(Sh.Reg("add \"HKCU\\Control Panel\\Desktop\" /v HungAppTimeout /t REG_SZ /d 5000 /f")); log(Sh.Reg("add \"HKLM\\SYSTEM\\CurrentControlSet\\Control\" /v WaitToKillServiceTimeout /t REG_SZ /d 5000 /f")); }));

            l.Add(T(CAT_RAM, "ADVANCED: Disable dynamic tick (bcdedit)",
                "Keeps the system timer ticking constantly instead of going tickless when idle. Some report smoother frame pacing; often placebo or slightly worse power draw on modern Windows. Requires reboot.",
                RiskLevel.Advanced, false,
                log => log(Sh.RunCmd("bcdedit /set disabledynamictick yes")),
                log => log(Sh.RunCmd("bcdedit /deletevalue disabledynamictick"))));

            l.Add(T(CAT_RAM, "ADVANCED: Force platform tick / TSC clock source (bcdedit)",
                "Changes the boot timer/clock source. Board/BIOS-specific; can help or hurt frame pacing. Requires reboot. Only change one timer setting at a time and compare.",
                RiskLevel.Advanced, false,
                log => { log(Sh.RunCmd("bcdedit /set useplatformclock false")); log(Sh.RunCmd("bcdedit /set useplatformtick yes")); },
                log => { log(Sh.RunCmd("bcdedit /set useplatformclock true")); log(Sh.RunCmd("bcdedit /deletevalue useplatformtick")); }));

            l.Add(T(CAT_RAM, "ADVANCED: Global timer resolution requests + distribute timers",
                "Lets any process request high-resolution timers and spreads timer interrupts across CPU cores. Marginal on modern hardware; kept for completeness.",
                RiskLevel.Advanced, false,
                log => { log(Sh.Reg("add \"HKLM\\SYSTEM\\CurrentControlSet\\Control\\Session Manager\\kernel\" /v GlobalTimerResolutionRequests /t REG_DWORD /d 1 /f")); log(Sh.Reg("add \"HKLM\\SYSTEM\\CurrentControlSet\\Control\\Session Manager\\kernel\" /v DistributeTimers /t REG_DWORD /d 1 /f")); },
                log => { log(Sh.Reg("delete \"HKLM\\SYSTEM\\CurrentControlSet\\Control\\Session Manager\\kernel\" /v GlobalTimerResolutionRequests /f")); log(Sh.Reg("delete \"HKLM\\SYSTEM\\CurrentControlSet\\Control\\Session Manager\\kernel\" /v DistributeTimers /f")); }));
        }
    }

    // ===================== UI =====================
    public class MainForm : Form
    {
        // Exact palette pulled from the real jewskitweaks.builtwithrocket.new CSS (:root tokens).
        static readonly Color AppBg = Color.FromArgb(0xF5, 0xF3, 0xEE);       // --card
        static readonly Color PanelBg = Color.FromArgb(0xF5, 0xF3, 0xEE);
        static readonly Color PanelBg2 = Color.FromArgb(0xF5, 0xF3, 0xEE);
        static readonly Color RowA = Color.FromArgb(0xF5, 0xF3, 0xEE);
        static readonly Color RowB = Color.FromArgb(0xF5, 0xF3, 0xEE);
        static readonly Color RowSelected = Color.FromArgb(0x7B, 0x2F, 0xBE);  // --primary (selected tweak fill)
        static readonly Color Border = Color.FromArgb(0x11, 0x11, 0x11);      // --border
        static readonly Color NavBg = Color.FromArgb(0xF5, 0xF3, 0xEE);
        static readonly Color NavHover = Color.FromArgb(0x2a, 0x2a, 0x2a);
        static readonly Color NavSelected = Color.FromArgb(0x7B, 0x2F, 0xBE);
        static readonly Color TextPrimary = Color.FromArgb(0x11, 0x11, 0x11); // --foreground
        static readonly Color TextMuted = Color.FromArgb(0x99, 0x99, 0x99);
        static readonly Color AccentPurple = Color.FromArgb(0x7B, 0x2F, 0xBE); // --primary
        static readonly Color AccentPurpleDark = Color.FromArgb(0x63, 0x25, 0x99);
        static readonly Color AccentPurpleDim = Color.FromArgb(0xC0, 0x84, 0xFC); // --accent (selected-tweak border)
        static readonly Color SafeColor = Color.FromArgb(0x2E, 0x7D, 0x32);    // --green-spec (GPU)
        static readonly Color ModerateColor = Color.FromArgb(0x3F, 0x51, 0xB5); // --blue-spec (CPU)
        static readonly Color AdvancedColor = Color.FromArgb(0xE5, 0x39, 0x35); // --red-spec (Motherboard)
        static readonly Color PillBlack = Color.FromArgb(0x11, 0x11, 0x11);    // .tab-btn-inactive / .tweak-btn-unselected
        static readonly Color PillBlackHover = Color.FromArgb(0x28, 0x28, 0x28);
        static readonly Color PillGreen = Color.FromArgb(0x1F, 0xBE, 0x6E);    // --secondary (Apply buttons)
        static readonly Color PillGreenDark = Color.FromArgb(0x18, 0x9A, 0x58);
        static readonly Color PillSelected = Color.FromArgb(0x7B, 0x2F, 0xBE); // .tweak-btn-selected
        static readonly Color DiscordBlurple = Color.FromArgb(0x58, 0x65, 0xF2);
        static readonly Color DividerSoft = Color.FromArgb(0xD0, 0xCC, 0xC4);  // .premium-divider

        // Georgia is the target (matches the real site's headings), but on a machine where
        // it's missing GDI+ silently substitutes without erroring - so this picks the first
        // family that's actually installed instead of trusting Georgia blindly.
        static string _serifFamily;
        static string SerifFamilyName()
        {
            if (_serifFamily != null) return _serifFamily;
            var installed = new HashSet<string>(FontFamily.Families.Select(f => f.Name), StringComparer.OrdinalIgnoreCase);
            foreach (var candidate in new[] { "Georgia", "Cambria", "Constantia", "Times New Roman" })
                if (installed.Contains(candidate)) { _serifFamily = candidate; return _serifFamily; }
            _serifFamily = FontFamily.GenericSerif.Name;
            return _serifFamily;
        }

        static Font SerifBold(float size)
        {
            return new Font(SerifFamilyName(), size, FontStyle.Bold);
        }

        // Toggle switch: track + thumb, click to flip. onChange fires with the new state.
        Panel MakeToggle(bool initial, Color onColor, Action<bool> onChange)
        {
            bool state = initial;
            Color offColor = Color.FromArgb(0xD8, 0xD4, 0xCB);
            var track = new Panel { Width = 40, Height = 22, Cursor = Cursors.Hand, BackColor = initial ? onColor : offColor };
            RoundCorners(track, 11);
            var thumb = new Panel { Width = 16, Height = 16, BackColor = Color.White, Location = new Point(initial ? 21 : 3, 3) };
            RoundCorners(thumb, 8);
            track.Controls.Add(thumb);
            EventHandler click = (s, e) => {
                state = !state;
                track.BackColor = state ? onColor : offColor;
                thumb.Location = new Point(state ? 21 : 3, 3);
                onChange(state);
            };
            track.Click += click; thumb.Click += click;
            track.Tag = new Func<bool>(() => state);
            return track;
        }

        static System.Drawing.Drawing2D.GraphicsPath RoundedRect(Rectangle r, int radius)
        {
            var path = new System.Drawing.Drawing2D.GraphicsPath();
            int d = radius * 2;
            if (d > r.Width) d = r.Width;
            if (d > r.Height) d = r.Height;
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        static void RoundCorners(Control c, int radius)
        {
            if (c.Width <= 0 || c.Height <= 0) return;
            using (var path = RoundedRect(new Rectangle(0, 0, c.Width, c.Height), radius))
                c.Region = new Region(path);
        }

        List<Tweak> _tweaks;
        FlowLayoutPanel _navFlow;
        Panel _contentHost;
        List<Panel> _pages = new List<Panel>();
        Panel _selectedNavItem;
        Dictionary<string, Panel> _categoryNavItems = new Dictionary<string, Panel>();
        Dictionary<string, Panel> _categoryPages = new Dictionary<string, Panel>();
        Panel _firstCategoryNav, _firstCategoryPage;

        // ---------- Native dark-mode theming (title bar + scrollbars) ----------
        [DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        static extern int SetWindowTheme(IntPtr hWnd, string pszSubAppName, string pszSubIdList);

        [DllImport("user32.dll")]
        static extern bool ShowScrollBar(IntPtr hWnd, int wBar, [MarshalAs(UnmanagedType.Bool)] bool bShow);
        const int SB_BOTH = 3;

        // The real site scrolls its panels with no visible scrollbar (CSS scrollbar-width:none) -
        // mouse-wheel scrolling still works, we just hide the native Win32 scrollbar that
        // AutoScroll panels draw. Windows re-shows it on every layout pass, so this has to
        // re-hide on every resize/layout, not just once at handle creation.
        static void DarkScroll(Control c)
        {
            Action hide = () => { try { if (c.IsHandleCreated) ShowScrollBar(c.Handle, SB_BOTH, false); } catch { } };
            c.HandleCreated += (s, e) => hide();
            c.Layout += (s, e) => hide();
            c.Resize += (s, e) => hide();
            if (c.IsHandleCreated) hide();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
        }

        // ---------- Embedded sound (MCI - plays MP3 with no extra dependencies) ----------
        [DllImport("winmm.dll")]
        static extern long mciSendString(string command, StringBuilder buffer, int bufferSize, IntPtr hwndCallback);

        static byte[] LoadEmbeddedBytes(string resourceName)
        {
            try
            {
                var asm = System.Reflection.Assembly.GetExecutingAssembly();
                using (var s = asm.GetManifestResourceStream(resourceName))
                {
                    if (s == null) return null;
                    using (var ms = new MemoryStream())
                    {
                        s.CopyTo(ms);
                        return ms.ToArray();
                    }
                }
            }
            catch { return null; }
        }

        static void PlaySound(string resourceName, string alias)
        {
            try
            {
                byte[] bytes = LoadEmbeddedBytes(resourceName);
                if (bytes == null) return;
                string tmp = Path.Combine(Path.GetTempPath(), alias + ".mp3");
                File.WriteAllBytes(tmp, bytes);
                mciSendString("close " + alias, null, 0, IntPtr.Zero);
                mciSendString("open \"" + tmp + "\" type mpegvideo alias " + alias, null, 0, IntPtr.Zero);
                mciSendString("play " + alias, null, 0, IntPtr.Zero);
            }
            catch { }
        }

        public MainForm()
        {
            _tweaks = Catalog.Build();
            Text = "Jewski Free Tweaks";
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            Width = 1340;
            Height = 880;
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 9F);
            BackColor = AppBg;
            ForeColor = TextPrimary;
            MinimumSize = new Size(1040, 660);

            BuildSidebarAndContent();
            BuildBusyOverlay();
        }

        // ---------- Busy overlay: spinning arc + live status text, shown over the whole
        // window during Apply/Revert/Fixes/restore-point so long-running operations aren't silent. ----------
        Panel _busyOverlay;
        Panel _busySpinner;
        Label _busyLabel;
        System.Windows.Forms.Timer _busyTimer;
        float _busyAngle;

        void BuildBusyOverlay()
        {
            _busyOverlay = new Panel { Dock = DockStyle.Fill, BackColor = AppBg, Visible = false };
            var center = new Panel { Size = new Size(280, 120) };
            _busySpinner = new Panel { Size = new Size(56, 56), Location = new Point((280 - 56) / 2, 0), BackColor = Color.Transparent };
            _busySpinner.Paint += (s, e) => {
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using (var pen = new Pen(AccentPurple, 5))
                {
                    pen.StartCap = System.Drawing.Drawing2D.LineCap.Round;
                    pen.EndCap = System.Drawing.Drawing2D.LineCap.Round;
                    e.Graphics.DrawArc(pen, new Rectangle(4, 4, 48, 48), _busyAngle, 110);
                }
            };
            _busyLabel = new Label { Text = "Working...", Font = SerifBold(13F), ForeColor = TextPrimary, TextAlign = ContentAlignment.MiddleCenter, Location = new Point(0, 68), Size = new Size(280, 50) };
            center.Controls.Add(_busySpinner);
            center.Controls.Add(_busyLabel);
            _busyOverlay.Controls.Add(center);
            Action reposition = () => { center.Location = new Point((_busyOverlay.Width - center.Width) / 2, (_busyOverlay.Height - center.Height) / 2); };
            _busyOverlay.Resize += (s, e) => reposition();
            reposition();

            _busyTimer = new System.Windows.Forms.Timer { Interval = 16 };
            _busyTimer.Tick += (s, e) => { _busyAngle = (_busyAngle + 6) % 360; _busySpinner.Invalidate(); };

            Controls.Add(_busyOverlay);
            _busyOverlay.BringToFront();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            PlaySound("JewskiTweaksGUI.Resources.launch.mp3", "jewskilaunch");
        }

        static Bitmap LoadEmbeddedLogo()
        {
            try
            {
                var asm = System.Reflection.Assembly.GetExecutingAssembly();
                using (var s = asm.GetManifestResourceStream("JewskiTweaksGUI.Resources.logo.png"))
                {
                    if (s == null) return null;
                    return new Bitmap(s);
                }
            }
            catch { return null; }
        }

        // Logo + "Jewski Tweaks" block that sits at the TOP OF THE SIDEBAR (not a full-width
        // banner) — matches the mockup, where the brand mark lives above the tab pills.
        Panel BuildSidebarLogo()
        {
            var block = new Panel { Dock = DockStyle.Top, Height = 128, BackColor = NavBg };
            var logoImg = LoadEmbeddedLogo();
            if (logoImg != null)
            {
                var logoBox = new PictureBox
                {
                    Image = logoImg,
                    SizeMode = PictureBoxSizeMode.Zoom,
                    Size = new Size(66, 66),
                    Location = new Point(18, 14),
                    BackColor = Color.Transparent
                };
                block.Controls.Add(logoBox);
            }
            var title = new Label
            {
                Text = "Jewski Tweaks",
                ForeColor = TextPrimary,
                Font = SerifBold(15F),
                AutoSize = true,
                BackColor = Color.Transparent,
                Location = new Point(16, 88)
            };
            block.Controls.Add(title);
            return block;
        }

        int CountCategories()
        {
            var set = new System.Collections.Generic.HashSet<string>();
            foreach (var t in _tweaks) set.Add(t.Category);
            return set.Count;
        }

        void BuildSidebarAndContent()
        {
            var body = new Panel { Dock = DockStyle.Fill, BackColor = AppBg };

            _contentHost = new Panel { Dock = DockStyle.Fill, BackColor = AppBg };

            var navOuter = new Panel { Dock = DockStyle.Left, Width = 250, BackColor = NavBg };
            var navRightBorder = new Panel { Dock = DockStyle.Right, Width = 1, BackColor = Border };
            var navLogo = BuildSidebarLogo();
            _navFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                BackColor = NavBg,
                Padding = new Padding(0, 6, 0, 8)
            };
            navOuter.Controls.Add(_navFlow);
            navOuter.Controls.Add(navLogo);
            navOuter.Controls.Add(navRightBorder);
            DarkScroll(_navFlow);
            _navFlow.Resize += (s, e) => FixNavItemWidths();

            var welcomePage = BuildWelcomePage();
            var welcomeNav = MakeNavItem("■", "HOME", null, welcomePage);
            _navFlow.Controls.Add(welcomeNav);
            _contentHost.Controls.Add(welcomePage);
            _navFlow.Controls.Add(MakeNavSeparator());
            Panel firstNavItem = welcomeNav, firstPage = welcomePage;

            var byCat = new Dictionary<string, List<Tweak>>();
            var order = new List<string>();
            foreach (var t in _tweaks)
            {
                if (!byCat.ContainsKey(t.Category)) { byCat[t.Category] = new List<Tweak>(); order.Add(t.Category); }
                byCat[t.Category].Add(t);
            }

            foreach (var cat in order)
            {
                var page = BuildTweakPage(cat, byCat[cat]);
                var navItem = MakeNavItem("●", cat, byCat[cat].Count.ToString(), page);
                _navFlow.Controls.Add(navItem);
                _contentHost.Controls.Add(page);
                _categoryNavItems[cat] = navItem;
                _categoryPages[cat] = page;
                if (_firstCategoryNav == null) { _firstCategoryNav = navItem; _firstCategoryPage = page; }
            }

            var startupPage = BuildStartupAppsPage();
            var startupNav = MakeNavItem("↻", "STARTUP APPS", null, startupPage);
            _navFlow.Controls.Add(MakeNavSeparator());
            _navFlow.Controls.Add(startupNav);
            _contentHost.Controls.Add(startupPage);

            var fixesPage = BuildFixesPage();
            var fixesNav = MakeNavItem("⚙", "FIXES", null, fixesPage);
            _navFlow.Controls.Add(fixesNav);
            _contentHost.Controls.Add(fixesPage);

            body.Controls.Add(_contentHost);
            body.Controls.Add(navOuter);
            Controls.Add(body);
            body.BringToFront();

            if (firstNavItem != null) SelectPage(firstNavItem, firstPage);

            FixNavItemWidths();
            Load += (s, e) => FixNavItemWidths();
        }

        // Kills the phantom horizontal scrollbar that FlowLayoutPanel(AutoScroll) grows
        // once its vertical scrollbar appears: items were sized for the wider pre-scrollbar
        // client area, so the panel thinks it also needs to scroll sideways. Re-sizing every
        // item to the CURRENT client width (post-scrollbar) removes that.
        void FixNavItemWidths()
        {
            if (_navFlow == null) return;
            int w = _navFlow.ClientSize.Width - 16;
            if (w <= 0) return;
            foreach (Control c in _navFlow.Controls)
            {
                c.Width = w;
                if (c is Panel && c.Tag is string) RoundCorners(c, c.Height / 2);
            }
            _navFlow.HorizontalScroll.Maximum = 0;
            _navFlow.HorizontalScroll.Visible = false;
        }

        Panel MakeNavSeparator()
        {
            var sep = new Panel { Width = 230, Height = 12, BackColor = NavBg, Margin = new Padding(8, 2, 8, 2) };
            return sep;
        }

        static string Disp(string s)
        {
            return string.IsNullOrEmpty(s) ? s : s.Replace("&&", "&");
        }

        Panel MakeNavItem(string icon, string label, string count, Panel page)
        {
            // icon is ignored - the real site's tab pills are plain centered text, no icons.
            string full = Disp(label);
            var item = new Panel { Width = 230, Height = 56, BackColor = PillBlack, Cursor = Cursors.Hand, Margin = new Padding(0, 0, 0, 12), Tag = "navpill" };
            var lbl = new Label
            {
                Text = full,
                UseMnemonic = false,
                AutoEllipsis = true,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };
            item.Controls.Add(lbl);
            RoundCorners(item, 28);
            EventHandler click = (s, e) => SelectPage(item, page);
            item.Click += click; lbl.Click += click;
            item.MouseEnter += (s, e) => { if (_selectedNavItem != item) item.BackColor = PillBlackHover; };
            item.MouseLeave += (s, e) => { if (_selectedNavItem != item) item.BackColor = PillBlack; };
            return item;
        }

        void SelectPage(Panel navItem, Panel page)
        {
            if (_selectedNavItem != null) _selectedNavItem.BackColor = PillBlack;
            navItem.BackColor = AccentPurple;
            _selectedNavItem = navItem;
            foreach (var p in _pages) p.Visible = (p == page);
            page.Visible = true;
            page.BringToFront();
        }

        void GoToCategory(string category)
        {
            Panel nav, page;
            if (category != null && _categoryNavItems.TryGetValue(category, out nav) && _categoryPages.TryGetValue(category, out page))
            {
                SelectPage(nav, page);
            }
            else if (_firstCategoryNav != null)
            {
                SelectPage(_firstCategoryNav, _firstCategoryPage);
            }
        }

        static string WmiFirst(string wql, string prop)
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher(wql))
                foreach (ManagementObject mo in searcher.Get())
                {
                    var v = mo[prop];
                    if (v != null && v.ToString().Trim().Length > 0) return v.ToString().Trim();
                }
            }
            catch { }
            return "Unknown";
        }

        // Win32_VideoController returns every adapter Windows knows about, including virtual
        // ones (Parsec/RDP/TeamViewer/etc. all register a fake display adapter) - picking
        // "the first row" is why the specs card sometimes shows a remote-desktop driver
        // instead of the real GPU. This filters those out, then prefers a real PCI device,
        // then prefers a discrete GPU over an integrated one, then the one with the most VRAM.
        static readonly string[] FakeGpuNeedles = {
            "parsec", "teamviewer", "anydesk", "splashtop", "vnc", "remote desktop",
            "remotefx", "rdp", "citrix", "indirect display", "virtual display",
            "microsoft basic display", "microsoft basic render", "microsoft remote display",
            "usb3.0 display", "displaylink", "meta virtual monitor", "virtualbox", "vmware"
        };
        static readonly string[] DiscreteGpuNeedles = { "nvidia", "geforce", "rtx", "gtx", "radeon", " rx ", "arc a" };

        static string WmiGpuName()
        {
            try
            {
                var candidates = new List<Tuple<string, bool, bool, ulong>>(); // name, isRealPci, isDiscrete, vram
                using (var searcher = new ManagementObjectSearcher("SELECT Name, AdapterRAM, PNPDeviceID FROM Win32_VideoController"))
                foreach (ManagementObject mo in searcher.Get())
                {
                    string name = (mo["Name"] ?? "").ToString().Trim();
                    if (name.Length == 0) continue;
                    string nameLower = name.ToLowerInvariant();
                    if (FakeGpuNeedles.Any(nameLower.Contains)) continue;
                    string pnp = (mo["PNPDeviceID"] ?? "").ToString();
                    bool isRealPci = pnp.StartsWith("PCI\\", StringComparison.OrdinalIgnoreCase);
                    bool isDiscrete = DiscreteGpuNeedles.Any(nameLower.Contains);
                    ulong vram = 0;
                    try { vram = Convert.ToUInt64(mo["AdapterRAM"] ?? 0UL); } catch { }
                    candidates.Add(Tuple.Create(name, isRealPci, isDiscrete, vram));
                }
                if (candidates.Count == 0) return "Unknown";
                var best = candidates
                    .OrderByDescending(c => c.Item2)   // real PCI hardware first
                    .ThenByDescending(c => c.Item3)    // discrete over integrated
                    .ThenByDescending(c => c.Item4)    // most VRAM
                    .First();
                return best.Item1;
            }
            catch { return "Unknown"; }
        }

        Panel MakePremiumDivider()
        {
            var div = new Panel { Dock = DockStyle.Top, Height = 18, BackColor = AppBg };
            div.Paint += (s, e) => {
                var rect = new Rectangle(0, 8, div.Width, 1);
                if (rect.Width <= 0) return;
                using (var brush = new System.Drawing.Drawing2D.LinearGradientBrush(new Rectangle(0, 0, Math.Max(rect.Width, 1), 1), DividerSoft, DividerSoft, 0f))
                {
                    var blend = new System.Drawing.Drawing2D.ColorBlend(3);
                    blend.Colors = new[] { Color.Transparent, DividerSoft, Color.Transparent };
                    blend.Positions = new[] { 0f, 0.5f, 1f };
                    brush.InterpolationColors = blend;
                    e.Graphics.FillRectangle(brush, rect);
                }
            };
            return div;
        }

        Label MakeEyebrow(string text)
        {
            return new Label { Text = text, Dock = DockStyle.Top, Height = 20, Font = new Font("Segoe UI", 8F, FontStyle.Bold), ForeColor = Color.FromArgb(0x99, 0x99, 0x99) };
        }

        Panel BuildWelcomePage()
        {
            var page = new Panel { Dock = DockStyle.Fill, Visible = false, BackColor = AppBg, Padding = new Padding(40, 40, 40, 40), AutoScroll = true };
            _pages.Add(page);

            // ---------- Header: logo + heading (left), MADE BY JEWSKI + restore button (right) ----------
            var headerRow = new Panel { Dock = DockStyle.Top, Height = 76 };
            var logoImg2 = LoadEmbeddedLogo();
            if (logoImg2 != null)
            {
                var logoBox2 = new PictureBox { Image = logoImg2, SizeMode = PictureBoxSizeMode.Zoom, Size = new Size(64, 64), Location = new Point(0, 0), BackColor = Color.Transparent };
                headerRow.Controls.Add(logoBox2);
            }
            var h1 = new Label { Text = "HOPEFULLY YOU'LL ENJOY", Font = SerifBold(20F), ForeColor = TextPrimary, Location = new Point(84, 2), AutoSize = true };
            var h1sub = new Label { Text = "JEWSKI TWEAKS \u2014 PERFORMANCE SUITE", Font = new Font("Segoe UI", 8F, FontStyle.Bold), ForeColor = Color.FromArgb(0x88, 0x88, 0x88), Location = new Point(86, 34), AutoSize = true };
            headerRow.Controls.Add(h1);
            headerRow.Controls.Add(h1sub);

            var madeByCol = new Panel { Dock = DockStyle.Right, Width = 230 };
            var madeBy = new Label { Text = "MADE BY JEWSKI", Font = SerifBold(15F), ForeColor = TextPrimary, Dock = DockStyle.Top, Height = 26, TextAlign = ContentAlignment.MiddleRight };
            var btnRestore2 = MakeGradientButton("CREATE RESTORE POINT");
            btnRestore2.Dock = DockStyle.Top;
            btnRestore2.Click += (s, e) => {
                SetBusy(true, "Creating restore point...");
                Task.Run(() => {
                    string r = Sh.RunPS("Enable-ComputerRestore -Drive 'C:\\' -EA SilentlyContinue; Checkpoint-Computer -Description 'Jewski Free Tweaks' -RestorePointType 'MODIFY_SETTINGS'");
                    AppendLog("[Restore Point] " + r);
                    Invoke((MethodInvoker)(() => { SetBusy(false, "Restore point step finished."); MessageBox.Show(this, "Restore point request sent.", "Jewski Free Tweaks"); }));
                });
            };
            madeByCol.Controls.Add(btnRestore2);
            madeByCol.Controls.Add(madeBy);
            headerRow.Controls.Add(madeByCol);

            var div1 = MakePremiumDivider();

            // ---------- Discord section ----------
            var communitySection = new Panel { Dock = DockStyle.Top, Height = 92 };
            var eyebrow1 = MakeEyebrow("COMMUNITY");
            var h2Discord = new Label { Text = "CLICK TO COPY", Dock = DockStyle.Top, Height = 32, Font = SerifBold(18F), ForeColor = TextPrimary };
            string discordUrl = "https://discord.gg/3c4B6ccV6Z";
            var discordRow = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 30, AutoSize = false, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
            var discordLabel = new Label { Text = "DISCORD LINK :  ", Font = SerifBold(13F), ForeColor = TextPrimary, AutoSize = true, Margin = new Padding(0) };
            var discordUrlLbl = new Label { Text = discordUrl, Font = SerifBold(13F), ForeColor = DiscordBlurple, AutoSize = true, Cursor = Cursors.Hand, Margin = new Padding(0) };
            var discordFont = discordUrlLbl.Font;
            discordUrlLbl.Paint += (s, e) => {
                var sz = e.Graphics.MeasureString(discordUrlLbl.Text, discordFont);
                int y = discordUrlLbl.Height - 3;
                using (var pen = new Pen(DiscordBlurple, 1.4f)) e.Graphics.DrawLine(pen, 0, y, sz.Width - 4, y);
            };
            var copyTimer = new System.Windows.Forms.Timer { Interval = 1400 };
            copyTimer.Tick += (s, e) => { discordUrlLbl.Text = discordUrl; discordUrlLbl.ForeColor = DiscordBlurple; copyTimer.Stop(); };
            EventHandler copyClick = (s, e) => {
                try { Clipboard.SetText(discordUrl); } catch { }
                discordUrlLbl.Text = "Copied to clipboard!";
                discordUrlLbl.ForeColor = PillGreen;
                copyTimer.Stop(); copyTimer.Start();
            };
            discordUrlLbl.Click += copyClick; discordLabel.Click += copyClick;
            discordRow.Controls.Add(discordLabel);
            discordRow.Controls.Add(discordUrlLbl);
            communitySection.Controls.Add(discordRow);
            communitySection.Controls.Add(h2Discord);
            communitySection.Controls.Add(eyebrow1);

            var div2 = MakePremiumDivider();

            // ---------- Computer specs section ----------
            var specsSection = new Panel { Dock = DockStyle.Top, Height = 190 };
            var eyebrow2 = MakeEyebrow("SYSTEM SPECS");
            var h2Specs = new Label { Text = "COMPUTER SPECS:", Dock = DockStyle.Top, Height = 32, Font = SerifBold(18F), ForeColor = TextPrimary };
            var specsGrid = new TableLayoutPanel { Dock = DockStyle.Top, Height = 130, ColumnCount = 2, RowCount = 2 };
            specsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            specsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            specsGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 62f));
            specsGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 62f));
            var gpuCard2 = MakeSpecCard("GPU", "Detecting...", SafeColor);
            var cpuCard2 = MakeSpecCard("CPU", "Detecting...", ModerateColor);
            var moboCard2 = MakeSpecCard("MOTHERBOARD", "Detecting...", AdvancedColor);
            specsGrid.Controls.Add(gpuCard2, 0, 0);
            specsGrid.Controls.Add(cpuCard2, 1, 0);
            specsGrid.SetColumnSpan(moboCard2, 2);
            specsGrid.Controls.Add(moboCard2, 0, 1);
            specsSection.Controls.Add(specsGrid);
            specsSection.Controls.Add(h2Specs);
            specsSection.Controls.Add(eyebrow2);

            page.Controls.Add(specsSection);
            page.Controls.Add(div2);
            page.Controls.Add(communitySection);
            page.Controls.Add(div1);
            page.Controls.Add(headerRow);

            Task.Run(() => {
                string cpu = WmiFirst("SELECT Name FROM Win32_Processor", "Name");
                string gpu = WmiGpuName();
                string mfr = WmiFirst("SELECT Manufacturer FROM Win32_BaseBoard", "Manufacturer");
                string prod = WmiFirst("SELECT Product FROM Win32_BaseBoard", "Product");
                string mobo = (mfr == "Unknown" ? "" : mfr + " ") + prod;
                try
                {
                    Invoke((MethodInvoker)(() => {
                        SetSpecCardValue(gpuCard2, gpu);
                        SetSpecCardValue(cpuCard2, cpu);
                        SetSpecCardValue(moboCard2, mobo);
                    }));
                }
                catch { }
            });

            return page;
        }

        Panel MakeSpecCard(string label, string value, Color valueColor)
        {
            var card = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 8, 8), BackColor = Color.FromArgb(10, 0, 0, 0), Padding = new Padding(20, 14, 20, 14) };
            var lbl = new Label { Text = label, Dock = DockStyle.Top, Height = 16, Font = new Font("Segoe UI", 7F, FontStyle.Bold), ForeColor = Color.FromArgb(0x99, 0x99, 0x99) };
            var val = new Label { Text = value, Name = "val", Dock = DockStyle.Top, Height = 28, Font = SerifBold(15F), ForeColor = valueColor, AutoEllipsis = true };
            card.Controls.Add(val);
            card.Controls.Add(lbl);
            return card;
        }

        static void SetSpecCardValue(Panel card, string value)
        {
            foreach (Control c in card.Controls) if (c.Name == "val") { c.Text = value; break; }
        }

        Button MakeGradientButton(string text)
        {
            var b = new Button
            {
                Text = text,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(18, 9, 18, 9),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(0x2d, 0x2d, 0x2d)
            };
            b.FlatAppearance.BorderSize = 0;
            b.FlatAppearance.MouseOverBackColor = Color.FromArgb(0x40, 0x40, 0x40);
            b.HandleCreated += (s, e) => RoundCorners(b, 10);
            b.SizeChanged += (s, e) => RoundCorners(b, 10);
            return b;
        }

        // Same phantom-scrollbar-safe pattern as the sidebar, but here it also enforces
        // a genuine fixed column count (3, per the mockup) instead of "however many fit."
        void FixGridCardWidths(FlowLayoutPanel grid, int columns)
        {
            int w = grid.ClientSize.Width;
            if (w <= 0) return;
            int cardW = (w / columns) - 18;
            if (cardW < 220) cardW = 220;
            foreach (Control c in grid.Controls)
            {
                if (!(c is Panel)) continue;
                c.Width = cardW;
                RoundCorners(c, 16);
            }
            grid.HorizontalScroll.Maximum = 0;
            grid.HorizontalScroll.Visible = false;
        }

        // Matches the real site's .tweak-btn-unselected exactly: solid #111111, no
        // visible border, no toggle/selection state - the card is purely informational,
        // the page-level Apply Recommended / Apply All buttons do the work.
        Panel MakeTweakCard(Tweak t, ToolTip tip)
        {
            var card = new Panel { Width = 300, Height = 92, Margin = new Padding(8), Cursor = Cursors.Hand, BackColor = PillBlack };
            var lbl = new Label
            {
                Text = Disp(t.Name),
                UseMnemonic = false,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Padding = new Padding(14, 4, 14, 4),
                BackColor = Color.Transparent
            };
            card.Controls.Add(lbl);
            RoundCorners(card, 16);

            string tipText = Disp(t.Name) + "\r\nRisk: " + t.Risk + "     Recommended: " + (t.Recommended ? "Yes" : "No") + "\r\n\r\n" + Disp(t.Desc);
            tip.SetToolTip(card, tipText);
            tip.SetToolTip(lbl, tipText);

            card.MouseEnter += (s, e) => card.BackColor = PillBlackHover;
            card.MouseLeave += (s, e) => card.BackColor = PillBlack;

            return card;
        }

        // Bottom row shared by every tweak/fix page: green "APPLY RECOMMENDED" pill (left),
        // a small muted helper line (center), green "APPLY ALL" pill (right) - exactly the
        // real site's per-tab footer, not a global cross-page batch bar.
        Panel MakeApplyFooter(Action applyRecommended, Action applyAll)
        {
            var footer = new Panel { Dock = DockStyle.Bottom, Height = 56, BackColor = AppBg, Padding = new Padding(0, 12, 0, 0) };
            var btnRec = MakePillButton("APPLY RECOMMENDED", PillGreen, PillGreenDark);
            var btnAll = MakePillButton("APPLY ALL", PillGreen, PillGreenDark);
            btnRec.Click += (s, e) => applyRecommended();
            btnAll.Click += (s, e) => applyAll();
            var helper = new Label
            {
                Text = "Make sure to make a restore point before starting anything",
                ForeColor = TextMuted,
                Font = new Font("Segoe UI", 8.5F),
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Fill
            };
            btnRec.Dock = DockStyle.Left;
            btnAll.Dock = DockStyle.Right;
            footer.Controls.Add(helper);
            footer.Controls.Add(btnAll);
            footer.Controls.Add(btnRec);
            return footer;
        }

        Panel BuildTweakPage(string category, List<Tweak> tweaks)
        {
            var page = new Panel { Dock = DockStyle.Fill, Visible = false, BackColor = AppBg, Padding = new Padding(20, 16, 20, 16) };
            _pages.Add(page);

            var catTitle = new Label
            {
                Text = Disp(category),
                UseMnemonic = false,
                Font = SerifBold(24F),
                ForeColor = TextPrimary,
                Dock = DockStyle.Top,
                Height = 50,
                TextAlign = ContentAlignment.MiddleLeft
            };

            var grid = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                AutoScroll = true,
                BackColor = AppBg,
                Padding = new Padding(2, 6, 2, 10)
            };
            DarkScroll(grid);
            grid.Resize += (s, e) => FixGridCardWidths(grid, 3);

            var tip = new ToolTip { AutoPopDelay = 20000, InitialDelay = 350, ReshowDelay = 100 };
            foreach (var t in tweaks)
            {
                grid.Controls.Add(MakeTweakCard(t, tip));
            }
            FixGridCardWidths(grid, 3);
            Load += (s, e) => FixGridCardWidths(grid, 3);

            var footer = MakeApplyFooter(
                () => ApplyTweaks(tweaks.Where(t => t.Recommended).ToList(), true),
                () => ApplyTweaks(tweaks, true));

            page.Controls.Add(grid);
            page.Controls.Add(footer);
            page.Controls.Add(catTitle);
            return page;
        }

        Panel BuildStartupAppsPage()
        {
            var page = new Panel { Dock = DockStyle.Fill, Visible = false, BackColor = AppBg, Padding = new Padding(20, 16, 20, 16) };
            _pages.Add(page);

            var pageTitle = new Label { Text = "STARTUP APPS", Font = SerifBold(24F), ForeColor = TextPrimary, Dock = DockStyle.Top, Height = 46 };
            var pageSub = new Label { Text = "Programs set to launch at Windows startup. Toggle to enable or disable each entry.", Font = new Font("Segoe UI", 8.5F, FontStyle.Italic), ForeColor = TextMuted, Dock = DockStyle.Top, Height = 24 };

            var headerRow = new Panel { Dock = DockStyle.Top, Height = 26 };
            var hName = new Label { Text = "APP NAME", Font = new Font("Segoe UI", 8F, FontStyle.Bold), ForeColor = TextMuted, Dock = DockStyle.Fill, TextAlign = ContentAlignment.BottomLeft, Padding = new Padding(8, 0, 0, 4) };
            var hStatus = new Label { Text = "STATUS", Font = new Font("Segoe UI", 8F, FontStyle.Bold), ForeColor = TextMuted, Dock = DockStyle.Right, Width = 64, TextAlign = ContentAlignment.BottomCenter, Padding = new Padding(0, 0, 0, 4) };
            var hPub = new Label { Text = "PUBLISHER", Font = new Font("Segoe UI", 8F, FontStyle.Bold), ForeColor = TextMuted, Dock = DockStyle.Right, Width = 220, TextAlign = ContentAlignment.BottomLeft, Padding = new Padding(0, 0, 0, 4) };
            headerRow.Controls.Add(hName); headerRow.Controls.Add(hPub); headerRow.Controls.Add(hStatus);
            var headerRule = new Panel { Dock = DockStyle.Top, Height = 1, BackColor = Border };

            var rowsPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, BackColor = AppBg, Padding = new Padding(0, 8, 0, 10) };
            DarkScroll(rowsPanel);
            rowsPanel.Resize += (s, e) => { foreach (Control c in rowsPanel.Controls) c.Width = rowsPanel.ClientSize.Width - 4; };

            Action refresh = () => {
                rowsPanel.Controls.Clear();
                bool zebra = false;
                foreach (var se in ScanStartup())
                {
                    var row = MakeStartupRow(se, zebra);
                    row.Width = rowsPanel.ClientSize.Width - 4;
                    rowsPanel.Controls.Add(row);
                    zebra = !zebra;
                }
            };

            page.Controls.Add(rowsPanel);
            page.Controls.Add(headerRule);
            page.Controls.Add(headerRow);
            page.Controls.Add(pageSub);
            page.Controls.Add(pageTitle);
            refresh();
            Load += (s, e) => { foreach (Control c in rowsPanel.Controls) c.Width = rowsPanel.ClientSize.Width - 4; };
            return page;
        }

        Panel MakeStartupRow(StartupEntry se, bool zebra)
        {
            var row = new Panel { Height = 46, Width = 600, BackColor = zebra ? RowB : RowA, Margin = new Padding(0, 0, 0, 2) };

            var toggleHost = new Panel { Dock = DockStyle.Right, Width = 64 };
            bool enabled = se.Scope != "User" || GetStartupApproved(se.Name);
            var toggle = MakeToggle(enabled, PillGreen, on => {
                SetStartupApproved(se.Name, on);
                AppendLog("[Startup] " + (on ? "Enabled: " : "Disabled: ") + se.Name);
            });
            toggle.Location = new Point((64 - toggle.Width) / 2, (46 - toggle.Height) / 2);
            if (se.Scope != "User")
            {
                toggle.Enabled = false;
                var machineTip = new ToolTip();
                machineTip.SetToolTip(toggle, "Machine-wide entry - change via Task Manager > Startup");
            }
            toggleHost.Controls.Add(toggle);

            var pub = new Label { Text = GetPublisher(se.Command), Font = new Font("Segoe UI", 9F), ForeColor = TextMuted, Dock = DockStyle.Right, Width = 220, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true, Padding = new Padding(0, 0, 8, 0) };
            var name = new Label { Text = Disp(se.Name), Font = new Font("Segoe UI", 9.5F, FontStyle.Bold), ForeColor = se.Scope == "User" ? TextPrimary : TextMuted, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true, Padding = new Padding(8, 0, 0, 0) };

            row.Controls.Add(name);
            row.Controls.Add(pub);
            row.Controls.Add(toggleHost);
            return row;
        }

        static string GetPublisher(string command)
        {
            try
            {
                string path = command;
                if (path.StartsWith("\""))
                {
                    int end = path.IndexOf('"', 1);
                    path = end > 0 ? path.Substring(1, end - 1) : path.Trim('"');
                }
                else
                {
                    int sp = path.IndexOf(' ');
                    if (sp > 0) path = path.Substring(0, sp);
                }
                path = Environment.ExpandEnvironmentVariables(path);
                if (!File.Exists(path)) return "Unknown";
                var info = FileVersionInfo.GetVersionInfo(path);
                return string.IsNullOrWhiteSpace(info.CompanyName) ? "Unknown" : info.CompanyName;
            }
            catch { return "Unknown"; }
        }

        static bool GetStartupApproved(string valueName)
        {
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run"))
                {
                    if (k == null) return true;
                    var val = k.GetValue(valueName) as byte[];
                    if (val == null || val.Length == 0) return true;
                    return val[0] == 2;
                }
            }
            catch { return true; }
        }

        Panel BuildFixesPage()
        {
            var page = new Panel { Dock = DockStyle.Fill, Visible = false, BackColor = AppBg, Padding = new Padding(20, 16, 20, 16) };
            _pages.Add(page);

            var pageTitle = new Label { Text = "FIXES", Font = SerifBold(24F), ForeColor = TextPrimary, Dock = DockStyle.Top, Height = 46 };
            var pageSub = new Label { Text = "Each button runs immediately on click — no toggle needed.", Font = new Font("Segoe UI", 8.5F, FontStyle.Italic), ForeColor = TextMuted, Dock = DockStyle.Top, Height = 24 };

            var grid = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = true, AutoScroll = true, BackColor = AppBg, Padding = new Padding(2, 6, 2, 10) };
            DarkScroll(grid);
            grid.Resize += (s, e) => FixGridCardWidths(grid, 3);
            var tip = new ToolTip { AutoPopDelay = 20000, InitialDelay = 350, ReshowDelay = 100 };

            AddFixCard(grid, tip, "Restore Microphone Access",
                "Removes any policy that force-denies mic access, sets consent to Allow, and restarts the audio services. Use this if apps suddenly lost microphone access.",
                () => {
                    AppendLog(Sh.Reg("delete \"HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\AppPrivacy\" /v LetAppsAccessMicrophone /f"));
                    AppendLog(Sh.Reg("add \"HKCU\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\CapabilityAccessManager\\ConsentStore\\microphone\" /v Value /t REG_SZ /d Allow /f"));
                    AppendLog(Sh.Reg("add \"HKLM\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\CapabilityAccessManager\\ConsentStore\\microphone\" /v Value /t REG_SZ /d Allow /f"));
                    AppendLog(Sh.Stop("AudioSrv")); AppendLog(Sh.Stop("AudioEndpointBuilder"));
                    AppendLog(Sh.Start("AudioEndpointBuilder")); AppendLog(Sh.Start("AudioSrv"));
                    return "Done. Restart your PC, then check Settings > Privacy & security > Microphone.";
                });

            AddFixCard(grid, tip, "Restart Audio Services",
                "Restarts AudioSrv and AudioEndpointBuilder. Use this if sound has stopped working or a device isn't showing up.",
                () => {
                    AppendLog(Sh.Stop("AudioSrv")); AppendLog(Sh.Stop("AudioEndpointBuilder"));
                    AppendLog(Sh.Start("AudioEndpointBuilder")); AppendLog(Sh.Start("AudioSrv"));
                    return "Audio services restarted.";
                });

            AddFixCard(grid, tip, "Restart Explorer.exe",
                "Kills and relaunches explorer.exe. Fixes a frozen taskbar, unresponsive Start menu, or a desktop that stopped updating — without needing to reboot.",
                () => {
                    AppendLog(Sh.RunCmd("taskkill /f /im explorer.exe"));
                    System.Threading.Thread.Sleep(800);
                    AppendLog(Sh.RunCmd("start explorer.exe"));
                    return "Explorer restarted.";
                });

            AddFixCard(grid, tip, "Rebuild Icon Cache",
                "Clears the corrupted icon cache database and restarts Explorer. Fixes wrong/blank/generic icons showing on desktop or in File Explorer.",
                () => {
                    AppendLog(Sh.RunCmd("taskkill /f /im explorer.exe"));
                    AppendLog(Sh.RunCmd("ie4uinit.exe -show"));
                    AppendLog(Sh.RunCmd("del /a /q \"%LOCALAPPDATA%\\IconCache.db\""));
                    AppendLog(Sh.RunCmd("del /a /q \"%LOCALAPPDATA%\\Microsoft\\Windows\\Explorer\\iconcache_*.db\""));
                    System.Threading.Thread.Sleep(500);
                    AppendLog(Sh.RunCmd("start explorer.exe"));
                    return "Icon cache cleared and Explorer restarted.";
                });

            page.Controls.Add(grid);
            page.Controls.Add(pageSub);
            page.Controls.Add(pageTitle);
            FixGridCardWidths(grid, 3);
            Load += (s, e) => FixGridCardWidths(grid, 3);
            return page;
        }

        void AddFixCard(FlowLayoutPanel grid, ToolTip tip, string title, string desc, Func<string> action)
        {
            var card = new Panel { Width = 300, Height = 92, BackColor = PillBlack, Cursor = Cursors.Hand, Margin = new Padding(8) };
            var lbl = new Label
            {
                Text = Disp(title),
                UseMnemonic = false,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Padding = new Padding(14, 4, 14, 4),
                BackColor = Color.Transparent
            };
            card.Controls.Add(lbl);
            RoundCorners(card, 16);
            tip.SetToolTip(card, Disp(desc));
            tip.SetToolTip(lbl, Disp(desc));

            EventHandler run = (s, e) => {
                card.BackColor = PillSelected;
                SetBusy(true, title + "...");
                Task.Run(() => {
                    string result = action();
                    Invoke((MethodInvoker)(() => {
                        SetBusy(false, result);
                        card.BackColor = PillBlack;
                        MessageBox.Show(this, result, "Jewski Free Tweaks");
                    }));
                });
            };
            card.Click += run; lbl.Click += run;
            card.MouseEnter += (s, e) => { if (card.BackColor == PillBlack) card.BackColor = PillBlackHover; };
            card.MouseLeave += (s, e) => { if (card.BackColor == PillBlackHover) card.BackColor = PillBlack; };
            grid.Controls.Add(card);
        }

        Button MakePillButton(string text, Color bg, Color hover)
        {
            var b = new Button
            {
                Text = text,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(18, 8, 18, 8),
                Margin = new Padding(0, 4, 10, 4),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                BackColor = bg,
                ForeColor = Color.White
            };
            b.FlatAppearance.BorderSize = 0;
            b.FlatAppearance.MouseOverBackColor = hover;
            b.HandleCreated += (s, e) => RoundCorners(b, b.Height);
            b.SizeChanged += (s, e) => RoundCorners(b, b.Height);
            return b;
        }

        // Applies (or reverts) exactly the tweaks passed in - each category page calls this
        // with either its own Recommended subset or its own full tweak list, matching the
        // real site's per-page "Apply Recommended" / "Apply All" buttons (no cross-page batch).
        void ApplyTweaks(List<Tweak> tweaks, bool apply)
        {
            if (tweaks.Count == 0) return;

            string verb = apply ? "apply" : "revert";
            var confirm = MessageBox.Show(this,
                "About to " + verb + " " + tweaks.Count + " tweak(s).\n\nMake sure you've created a restore point first.\n\nContinue?",
                "Jewski Free Tweaks", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (confirm != DialogResult.Yes) return;

            SetBusy(true, (apply ? "Applying " : "Reverting ") + tweaks.Count + " tweak(s)...");

            Task.Run(() => {
                int i = 0;
                foreach (var t in tweaks)
                {
                    i++;
                    SetBusy(true, (apply ? "Applying " : "Reverting ") + i + "/" + tweaks.Count + ": " + Disp(t.Name));
                    AppendLog("==== " + (apply ? "APPLY" : "REVERT") + ": " + t.Name + " ====");
                    try { if (apply) t.Apply(AppendLog); else t.Revert(AppendLog); }
                    catch (Exception ex) { AppendLog("EXCEPTION: " + ex.Message); }
                }
                if (apply) PlaySound("JewskiTweaksGUI.Resources.done.mp3", "jewskidone");
                Invoke((MethodInvoker)(() => {
                    SetBusy(false, null);
                    MessageBox.Show(this, (apply ? "Applied " : "Reverted ") + tweaks.Count + " tweak(s).\n\nRestart your PC for everything to take full effect.", "Jewski Free Tweaks");
                }));
            });
        }

        void SetBusy(bool busy, string status)
        {
            if (InvokeRequired) { Invoke((MethodInvoker)(() => SetBusy(busy, status))); return; }
            _busyLabel.Text = status ?? "Working...";
            _busyOverlay.Visible = busy;
            if (busy) { _busyOverlay.BringToFront(); _busyTimer.Start(); }
            else { _busyTimer.Stop(); }
            Cursor = busy ? Cursors.WaitCursor : Cursors.Default;
        }

        static readonly object _logFileLock = new object();

        static void AppendLog(string line)
        {
            if (string.IsNullOrEmpty(line)) return;
            try
            {
                lock (_logFileLock)
                {
                    File.AppendAllText(Path.Combine(Path.GetTempPath(), "JewskiFreeTweaks_log.txt"), DateTime.Now + "  " + line + "\r\n");
                }
            }
            catch { }
        }

        // ---------- Startup scanning ----------
        class StartupEntry { public string Name; public string Scope; public string Command; }

        static List<StartupEntry> ScanStartup()
        {
            var results = new List<StartupEntry>();
            ScanKey(results, Registry.CurrentUser, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", "User");
            ScanKey(results, Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", "Machine");
            ScanKey(results, Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Run", "Machine");
            return results;
        }

        static void ScanKey(List<StartupEntry> results, RegistryKey root, string path, string scope)
        {
            try
            {
                using (var k = root.OpenSubKey(path))
                {
                    if (k == null) return;
                    foreach (var name in k.GetValueNames())
                    {
                        object v = k.GetValue(name);
                        results.Add(new StartupEntry { Name = name, Scope = scope, Command = v == null ? "" : v.ToString() });
                    }
                }
            }
            catch { }
        }

        static void SetStartupApproved(string valueName, bool enabled)
        {
            using (var k = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run"))
            {
                byte[] val = enabled
                    ? new byte[] { 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }
                    : new byte[] { 3, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
                if (k != null) k.SetValue(valueName, val, RegistryValueKind.Binary);
            }
        }
    }

    // Plays the embedded launch video full-screen-ish before the main window shows, via a
    // WPF MediaElement hosted in a WinForms ElementHost (Media Foundation decodes the mp4 -
    // no external codec/ffmpeg needed). Click, any key, or an 8s timeout skips it, and any
    // failure here (missing codec, bad resource, etc.) is swallowed by the caller so a splash
    // problem can never stop the real app from launching.
    public class SplashForm : Form
    {
        System.Windows.Controls.MediaElement _media;

        public SplashForm(string videoPath)
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.Black;
            ShowInTaskbar = false;
            TopMost = true;
            Width = 720;
            Height = 405;

            var host = new System.Windows.Forms.Integration.ElementHost { Dock = DockStyle.Fill, BackColorTransparent = false, BackColor = Color.Black };
            _media = new System.Windows.Controls.MediaElement
            {
                LoadedBehavior = System.Windows.Controls.MediaState.Manual,
                UnloadedBehavior = System.Windows.Controls.MediaState.Manual,
                Stretch = System.Windows.Media.Stretch.Uniform,
                Source = new Uri(videoPath)
            };
            host.Child = _media;
            Controls.Add(host);

            _media.MediaEnded += (s, e) => Close();
            _media.MediaFailed += (s, e) => Close();
            Shown += (s, e) => { try { _media.Play(); } catch { Close(); } };

            EventHandler skip = (s, e) => Close();
            Click += skip;
            host.Click += skip;
            KeyPress += (s, e) => Close();
            KeyPreview = true;

            var timeout = new System.Windows.Forms.Timer { Interval = 8000 };
            timeout.Tick += (s, e) => { timeout.Stop(); Close(); };
            timeout.Start();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            try { _media.Close(); } catch { }
            base.OnFormClosed(e);
        }
    }

    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.ThreadException += (s, e) => ShowFatal(e.Exception);
            AppDomain.CurrentDomain.UnhandledException += (s, e) => ShowFatal(e.ExceptionObject as Exception);

            try { ShowSplash(); } catch { }

            try
            {
                Application.Run(new MainForm());
            }
            catch (Exception ex)
            {
                ShowFatal(ex);
            }
        }

        static void ShowSplash()
        {
            var asm = System.Reflection.Assembly.GetExecutingAssembly();
            using (var s = asm.GetManifestResourceStream("JewskiTweaksGUI.Resources.launch.mp4"))
            {
                if (s == null) return;
                string tmp = Path.Combine(Path.GetTempPath(), "jewski_launch.mp4");
                using (var fs = File.Create(tmp)) s.CopyTo(fs);
                using (var splash = new SplashForm(tmp))
                {
                    Application.Run(splash);
                }
            }
        }

        static void ShowFatal(Exception ex)
        {
            string msg = ex == null ? "Unknown error (no exception details available)." : ex.ToString();
            try
            {
                System.IO.File.WriteAllText(
                    System.IO.Path.Combine(System.IO.Path.GetTempPath(), "JewskiFreeTweaks_crash.txt"),
                    DateTime.Now + "\r\n" + msg);
            }
            catch { }
            MessageBox.Show(msg, "Jewski Free Tweaks - Fatal Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
