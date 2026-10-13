using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Eto.Forms;
using OpenTabletDriver.Desktop.Contracts;
using OpenTabletDriver.Desktop.RPC;
using OpenTabletDriver.Plugin.Logging;
using OpenTabletDriver.Plugin.Tablet;

namespace OpenTabletDriver.UX.RPC
{
    public class DaemonRpcClient : RpcClient<IDriverDaemon>
    {
        public DaemonRpcClient(string pipeName) : base(pipeName)
        {
        }

        public event EventHandler<LogMessage>? Message;
        public event EventHandler<DebugReportData>? DeviceReport;
        public event EventHandler<IEnumerable<TabletReference>>? TabletsChanged;
        public event EventHandler? Resynchronize;

        /// <summary>
        /// The tablets the daemon reported last: fetched on connect, kept current by <see cref="TabletsChanged"/>, cleared on disconnect.
        /// </summary>
        public IReadOnlyList<TabletReference> Tablets { get; private set; } = [];

        // Instance is set before Connected is raised and never reset afterwards.
        public async Task RefreshTablets() => Tablets = [.. await Instance!.GetTablets()];

        protected override void OnDisconnected()
        {
            // On the UI thread, where TabletsChanged writes the list too.
            Application.Instance.AsyncInvoke(() => Tablets = []);
            base.OnDisconnected();
        }

        protected override void OnConnected()
        {
            Debug.Assert(IsConnected, $"{nameof(OnConnected)} called without being connected");
            base.OnConnected();

            Instance.Message += (sender, e) =>
                Application.Instance.AsyncInvoke(() => Message?.Invoke(sender, e));
            Instance.DeviceReport += (sender, e) =>
                Application.Instance.AsyncInvoke(() => DeviceReport?.Invoke(sender, e));
            Instance.TabletsChanged += (sender, e) =>
                Application.Instance.AsyncInvoke(() =>
                {
                    Tablets = [.. e];
                    TabletsChanged?.Invoke(sender, e);
                });
            Instance.Resynchronize += (sender, e) =>
                Application.Instance.AsyncInvoke(() => Resynchronize?.Invoke(sender, e));
        }
    }
}
