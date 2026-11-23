// API endpoint constants for ControlHub M1.
// Base URL and token are stored in Keychain; this file only contains paths.

class ApiEndpoints {
  ApiEndpoints._();

  static const String health = '/api/v1/health';
  static const String deviceStatus = '/api/v1/device/status';
  static const String capabilities = '/api/v1/device/capabilities';
  static const String powerLock = '/api/v1/power/lock';
  static const String powerSleep = '/api/v1/power/sleep';
  static const String powerShutdown = '/api/v1/power/shutdown';
  static const String powerRestart = '/api/v1/power/restart';
  static const String powerPending = '/api/v1/power/pending';
  static const String powerCancel = '/api/v1/power/cancel';
}

class AppDefaults {
  AppDefaults._();

  static const String baseUrl = 'http://127.0.0.1:8765';
  static const Duration connectTimeout = Duration(seconds: 5);
  static const Duration receiveTimeout = Duration(seconds: 10);
  static const Duration pollInterval = Duration(seconds: 5);
  static const int powerDelaySeconds = 30;
}
