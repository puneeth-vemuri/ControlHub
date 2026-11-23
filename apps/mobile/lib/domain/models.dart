/// Domain models for ControlHub M1.
/// All factories handle null/missing fields gracefully — the app will never
/// crash on a malformed or partial API response.
library;

enum DeviceConnectionState { online, offline, unknown }

class DeviceStatus {
  final String host;
  final String os;
  final String agent;
  final String companion;
  final int uptimeSeconds;
  final DeviceConnectionState connectionState;

  const DeviceStatus({
    required this.host,
    required this.os,
    required this.agent,
    required this.companion,
    required this.uptimeSeconds,
    required this.connectionState,
  });

  factory DeviceStatus.fromJson(Map<String, dynamic> j) => DeviceStatus(
        host: j['host'] as String? ?? 'Unknown',
        os: j['os'] as String? ?? 'Unknown',
        agent: j['agent'] as String? ?? 'unknown',
        companion: j['companion'] as String? ?? 'unavailable',
        uptimeSeconds: j['uptime_seconds'] as int? ?? 0,
        connectionState: DeviceConnectionState.online,
      );

  static DeviceStatus offline() => const DeviceStatus(
        host: 'Unknown',
        os: '',
        agent: 'offline',
        companion: 'unavailable',
        uptimeSeconds: 0,
        connectionState: DeviceConnectionState.offline,
      );

  static DeviceStatus unknown() => const DeviceStatus(
        host: 'Not configured',
        os: '',
        agent: 'unknown',
        companion: 'unavailable',
        uptimeSeconds: 0,
        connectionState: DeviceConnectionState.unknown,
      );

  /// Human-readable uptime string; never crashes on zero.
  String get uptimeFormatted {
    final d = Duration(seconds: uptimeSeconds);
    if (d.inDays > 0) return '${d.inDays}d ${d.inHours.remainder(24)}h';
    if (d.inHours > 0) return '${d.inHours}h ${d.inMinutes.remainder(60)}m';
    return '${d.inMinutes}m';
  }
}

class Capabilities {
  final bool shutdown;
  final bool restart;
  final bool sleep;
  final bool hibernate;
  final bool lock;
  final bool cancel;
  final bool companion;

  const Capabilities({
    this.shutdown = false,
    this.restart = false,
    this.sleep = false,
    this.hibernate = false,
    this.lock = false,
    this.cancel = false,
    this.companion = false,
  });

  factory Capabilities.fromJson(Map<String, dynamic> j) {
    final power = j['power'] as Map<String, dynamic>? ?? {};
    return Capabilities(
      shutdown: power['shutdown'] as bool? ?? false,
      restart: power['restart'] as bool? ?? false,
      sleep: power['sleep'] as bool? ?? false,
      hibernate: power['hibernate'] as bool? ?? false,
      lock: power['lock'] as bool? ?? false,
      cancel: power['cancel'] as bool? ?? false,
      companion: j['companion'] as bool? ?? false,
    );
  }
}

class PendingOperation {
  final String operationId;
  final String action;
  final String scheduledFor;
  final bool cancellable;
  final String state;

  const PendingOperation({
    required this.operationId,
    required this.action,
    required this.scheduledFor,
    required this.cancellable,
    required this.state,
  });

  factory PendingOperation.fromJson(Map<String, dynamic> j) => PendingOperation(
        operationId: j['operation_id'] as String? ?? '',
        action: j['action'] as String? ?? '',
        scheduledFor: j['scheduled_for'] as String? ?? '',
        cancellable: j['cancellable'] as bool? ?? false,
        state: j['state'] as String? ?? '',
      );
}
