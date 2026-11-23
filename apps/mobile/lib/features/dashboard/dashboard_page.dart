import 'package:flutter/cupertino.dart';
import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import '../../domain/models.dart';
import '../power/power_dialog.dart';
import '../power/power_provider.dart';
import 'dashboard_provider.dart';

const ink = Color(0xFF0A0D12);
const panel = Color(0xFF131922);
const muted = Color(0xFF8E9BAB);
const mint = Color(0xFF8CF0C5);

class DashboardPage extends StatelessWidget {
  const DashboardPage({super.key});

  @override
  Widget build(BuildContext context) {
    final dashboard = context.watch<DashboardProvider>();
    final power = context.watch<PowerProvider>();

    return CustomScrollView(
      slivers: [
        SliverPadding(
          padding: const EdgeInsets.fromLTRB(22, 20, 22, 28),
          sliver: SliverList.list(
            children: [
              Row(
                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                children: [
                  Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      const Text(
                        'CONTROLHUB',
                        style: TextStyle(
                          color: muted,
                          fontSize: 11,
                          letterSpacing: 2.4,
                          fontWeight: FontWeight.w700,
                        ),
                      ),
                      const SizedBox(height: 8),
                      Text(
                        'Laptop Control',
                        style: Theme.of(context).textTheme.headlineMedium?.copyWith(
                              fontWeight: FontWeight.w700,
                              letterSpacing: -.7,
                            ),
                      ),
                    ],
                  ),
                  Container(
                    width: 43,
                    height: 43,
                    decoration: BoxDecoration(
                      color: panel,
                      borderRadius: BorderRadius.circular(15),
                    ),
                    child: const Icon(CupertinoIcons.person_crop_circle, color: mint),
                  ),
                ],
              ),
              const SizedBox(height: 25),
              _DeviceCard(status: dashboard.status, configured: dashboard.configured),
              if (dashboard.pendingOp != null && dashboard.pendingOp!.state == 'accepted') ...[
                const SizedBox(height: 16),
                _PendingBanner(
                  op: dashboard.pendingOp!,
                  onCancel: () async {
                    final navigator = Navigator.of(context);
                    final ok = await power.cancelPending();
                    await dashboard.refresh();
                    if (!ok && power.lastError != null && navigator.mounted) {
                      _showError(navigator.context, power.lastError!);
                    }
                  },
                ),
              ],
              const SizedBox(height: 18),
              Row(
                children: const [
                  Expanded(
                    child: _MetricCard(
                      icon: CupertinoIcons.speedometer,
                      label: 'CPU',
                      value: '—',
                      note: 'Waiting for M3',
                    ),
                  ),
                  SizedBox(width: 12),
                  Expanded(
                    child: _MetricCard(
                      icon: Icons.memory,
                      label: 'MEMORY',
                      value: '—',
                      note: 'Waiting for M3',
                    ),
                  ),
                ],
              ),
              const SizedBox(height: 12),
              Row(
                children: const [
                  Expanded(
                    child: _MetricCard(
                      icon: CupertinoIcons.battery_75_percent,
                      label: 'BATTERY',
                      value: '—',
                      note: 'Waiting for M3',
                    ),
                  ),
                  SizedBox(width: 12),
                  Expanded(
                    child: _MetricCard(
                      icon: CupertinoIcons.wifi,
                      label: 'NETWORK',
                      value: '—',
                      note: 'Tailscale (M2)',
                    ),
                  ),
                ],
              ),
              const SizedBox(height: 26),
              Row(
                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                children: [
                  Text(
                    'Quick actions',
                    style: Theme.of(context).textTheme.titleLarge?.copyWith(fontWeight: FontWeight.w700),
                  ),
                  Text(
                    dashboard.configured ? 'Active' : 'Configure in Settings',
                    style: const TextStyle(color: muted, fontSize: 11),
                  ),
                ],
              ),
              const SizedBox(height: 14),
              Row(
                children: [
                  Expanded(
                    child: _ActionTile(
                      icon: CupertinoIcons.lock,
                      title: 'Lock',
                      subtitle: 'Workstation',
                      onTap: () async {
                        final navigator = Navigator.of(context);
                        final ok = await power.performAction('lock');
                        await dashboard.refresh();
                        if (!ok && power.lastError != null && navigator.mounted) {
                          _showError(navigator.context, power.lastError!);
                        }
                      },
                    ),
                  ),
                  const SizedBox(width: 11),
                  Expanded(
                    child: _ActionTile(
                      icon: CupertinoIcons.moon,
                      title: 'Sleep',
                      subtitle: 'Suspend laptop',
                      onTap: () async {
                        final navigator = Navigator.of(context);
                        final confirm = await showPowerConfirmation(
                          navigator.context,
                          action: 'sleep',
                          description: 'Putting laptop to sleep will disconnect the agent until manually awakened.',
                        );
                        if (confirm) {
                          final ok = await power.performAction('sleep');
                          await dashboard.refresh();
                          if (!ok && power.lastError != null && navigator.mounted) {
                            _showError(navigator.context, power.lastError!);
                          }
                        }
                      },
                    ),
                  ),
                  const SizedBox(width: 11),
                  Expanded(
                    child: _ActionTile(
                      icon: CupertinoIcons.power,
                      title: 'Power',
                      subtitle: 'Shutdown/Restart',
                      onTap: () => _showPowerMenu(context, dashboard, power),
                    ),
                  ),
                ],
              ),
              const SizedBox(height: 23),
              Container(
                padding: const EdgeInsets.all(17),
                decoration: BoxDecoration(color: panel, borderRadius: BorderRadius.circular(20)),
                child: Row(
                  children: [
                    Container(
                      width: 38,
                      height: 38,
                      decoration: BoxDecoration(
                        color: mint.withValues(alpha: .12),
                        borderRadius: BorderRadius.circular(13),
                      ),
                      child: const Icon(CupertinoIcons.shield_fill, color: mint, size: 19),
                    ),
                    const SizedBox(width: 12),
                    const Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text('Loopback & Private Tailnet', style: TextStyle(fontWeight: FontWeight.w700)),
                          SizedBox(height: 4),
                          Text(
                            'ControlHub binds to 127.0.0.1. No public exposure or open ports.',
                            style: TextStyle(color: muted, fontSize: 12, height: 1.35),
                          ),
                        ],
                      ),
                    ),
                  ],
                ),
              ),
            ],
          ),
        ),
      ],
    );
  }

  void _showPowerMenu(BuildContext context, DashboardProvider dashboard, PowerProvider power) {
    showCupertinoModalPopup(
      context: context,
      builder: (ctx) => CupertinoActionSheet(
        title: const Text('Power Options'),
        message: const Text('All operations initiate a 30-second cancellable delay.'),
        actions: [
          CupertinoActionSheetAction(
            isDestructiveAction: true,
            onPressed: () async {
              Navigator.of(ctx).pop();
              final navigator = Navigator.of(context);
              final confirm = await showPowerConfirmation(
                navigator.context,
                action: 'shutdown',
                description: 'The laptop will shut down in 30 seconds. You can cancel during this grace period.',
              );
              if (confirm) {
                final ok = await power.performAction('shutdown', delaySeconds: 30);
                await dashboard.refresh();
                if (!ok && power.lastError != null && navigator.mounted) {
                  _showError(navigator.context, power.lastError!);
                }
              }
            },
            child: const Text('Shutdown (30s delay)'),
          ),
          CupertinoActionSheetAction(
            isDestructiveAction: true,
            onPressed: () async {
              Navigator.of(ctx).pop();
              final navigator = Navigator.of(context);
              final confirm = await showPowerConfirmation(
                navigator.context,
                action: 'restart',
                description: 'The laptop will restart in 30 seconds. You can cancel during this grace period.',
              );
              if (confirm) {
                final ok = await power.performAction('restart', delaySeconds: 30);
                await dashboard.refresh();
                if (!ok && power.lastError != null && navigator.mounted) {
                  _showError(navigator.context, power.lastError!);
                }
              }
            },
            child: const Text('Restart (30s delay)'),
          ),
          CupertinoActionSheetAction(
            onPressed: () async {
              Navigator.of(ctx).pop();
              final navigator = Navigator.of(context);
              final confirm = await showPowerConfirmation(
                navigator.context,
                action: 'hibernate',
                description: 'The laptop will save state to disk and power off.',
              );
              if (confirm) {
                final ok = await power.performAction('hibernate');
                await dashboard.refresh();
                if (!ok && power.lastError != null && navigator.mounted) {
                  _showError(navigator.context, power.lastError!);
                }
              }
            },
            child: const Text('Hibernate'),
          ),
        ],
        cancelButton: CupertinoActionSheetAction(
          onPressed: () => Navigator.of(ctx).pop(),
          child: const Text('Cancel'),
        ),
      ),
    );
  }

  static void _showError(BuildContext context, String message) {
    showCupertinoDialog(
      context: context,
      builder: (ctx) => CupertinoAlertDialog(
        title: const Text('Action Failed'),
        content: Text(message),
        actions: [
          CupertinoDialogAction(
            child: const Text('OK'),
            onPressed: () => Navigator.of(ctx).pop(),
          ),
        ],
      ),
    );
  }
}

class _DeviceCard extends StatelessWidget {
  final DeviceStatus status;
  final bool configured;

  const _DeviceCard({required this.status, required this.configured});

  @override
  Widget build(BuildContext context) {
    Color statusColor;
    String statusText;

    if (!configured) {
      statusColor = const Color(0xFFFFC875);
      statusText = 'Not configured';
    } else if (status.connectionState == DeviceConnectionState.online) {
      statusColor = mint;
      statusText = 'Online · Uptime ${status.uptimeFormatted}';
    } else {
      statusColor = const Color(0xFFFF6B6B);
      statusText = 'Offline';
    }

    return Container(
      padding: const EdgeInsets.all(20),
      decoration: BoxDecoration(
        gradient: const LinearGradient(
          colors: [Color(0xFF1A2827), Color(0xFF18202A)],
          begin: Alignment.topLeft,
          end: Alignment.bottomRight,
        ),
        borderRadius: BorderRadius.circular(25),
        border: Border.all(color: const Color(0xFF33423F)),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Container(
                width: 42,
                height: 42,
                decoration: BoxDecoration(
                  color: Colors.white.withValues(alpha: .06),
                  borderRadius: BorderRadius.circular(14),
                ),
                child: const Icon(CupertinoIcons.device_laptop, color: mint),
              ),
              const SizedBox(width: 12),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      status.host.isNotEmpty && status.host != 'Unknown' ? status.host : 'Windows Laptop',
                      style: const TextStyle(fontSize: 16, fontWeight: FontWeight.w700),
                    ),
                    const SizedBox(height: 4),
                    Text(
                      status.os.isNotEmpty ? status.os : 'Windows 11',
                      style: const TextStyle(color: muted, fontSize: 12),
                    ),
                  ],
                ),
              ),
            ],
          ),
          const SizedBox(height: 21),
          Row(
            children: [
              Container(
                width: 8,
                height: 8,
                decoration: BoxDecoration(color: statusColor, shape: BoxShape.circle),
              ),
              const SizedBox(width: 8),
              Text(
                statusText,
                style: TextStyle(color: statusColor, fontSize: 12, fontWeight: FontWeight.w600),
              ),
            ],
          ),
        ],
      ),
    );
  }
}

class _PendingBanner extends StatelessWidget {
  final PendingOperation op;
  final VoidCallback onCancel;

  const _PendingBanner({required this.op, required this.onCancel});

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        color: const Color(0xFF3B2914),
        border: Border.all(color: const Color(0xFFD97706)),
        borderRadius: BorderRadius.circular(16),
      ),
      child: Row(
        children: [
          const Icon(CupertinoIcons.exclamationmark_triangle_fill, color: Color(0xFFFBBF24), size: 20),
          const SizedBox(width: 10),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  '${op.action.toUpperCase()} pending',
                  style: const TextStyle(fontWeight: FontWeight.bold, color: Color(0xFFFDE68A)),
                ),
                Text(
                  'Scheduled: ${op.scheduledFor.split('T').last.split('.').first}',
                  style: const TextStyle(color: Color(0xFFD1D5DB), fontSize: 12),
                ),
              ],
            ),
          ),
          CupertinoButton(
            padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 8),
            color: const Color(0xFFDC2626),
            borderRadius: BorderRadius.circular(10),
            onPressed: onCancel,
            child: const Text('Cancel', style: TextStyle(color: Colors.white, fontSize: 13, fontWeight: FontWeight.bold)),
          ),
        ],
      ),
    );
  }
}

class _MetricCard extends StatelessWidget {
  const _MetricCard({required this.icon, required this.label, required this.value, required this.note});
  final IconData icon;
  final String label, value, note;

  @override
  Widget build(BuildContext context) => Container(
        padding: const EdgeInsets.all(15),
        decoration: BoxDecoration(
          color: panel,
          borderRadius: BorderRadius.circular(20),
          border: Border.all(color: Colors.white.withValues(alpha: .035)),
        ),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                Text(
                  label,
                  style: const TextStyle(
                    color: muted,
                    fontSize: 10,
                    letterSpacing: 1.1,
                    fontWeight: FontWeight.w700,
                  ),
                ),
                Icon(icon, color: mint.withValues(alpha: .85), size: 17),
              ],
            ),
            const SizedBox(height: 14),
            Text(value, style: const TextStyle(fontSize: 25, fontWeight: FontWeight.w700, height: 1)),
            const SizedBox(height: 7),
            Text(note, style: const TextStyle(color: muted, fontSize: 10)),
          ],
        ),
      );
}

class _ActionTile extends StatelessWidget {
  const _ActionTile({required this.icon, required this.title, required this.subtitle, this.onTap});
  final IconData icon;
  final String title, subtitle;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) => GestureDetector(
        onTap: onTap,
        child: Container(
          padding: const EdgeInsets.fromLTRB(12, 13, 8, 13),
          decoration: BoxDecoration(color: panel, borderRadius: BorderRadius.circular(19)),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Icon(icon, color: mint, size: 19),
              const SizedBox(height: 13),
              Text(title, style: const TextStyle(fontWeight: FontWeight.w700, fontSize: 13)),
              const SizedBox(height: 4),
              Text(
                subtitle,
                style: const TextStyle(color: muted, fontSize: 9),
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
              ),
            ],
          ),
        ),
      );
}
