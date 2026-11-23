import 'dart:async';
import 'package:flutter/foundation.dart';
import '../../core/api_client.dart';
import '../../core/secure_storage.dart';
import '../../core/constants.dart';
import '../../domain/models.dart';

enum LoadingState { idle, loading, loaded, error }

/// Provides live device status, capabilities and pending operation data.
/// Polling starts on [initialize] and stops on [dispose].
class DashboardProvider extends ChangeNotifier {
  ApiClient? _client;

  DeviceStatus _status = DeviceStatus.unknown();
  Capabilities _caps = const Capabilities();
  PendingOperation? _pendingOp;
  LoadingState _loadingState = LoadingState.idle;
  String? _errorMessage;
  bool _configured = false;
  Timer? _refreshTimer;

  DeviceStatus get status => _status;
  Capabilities get capabilities => _caps;
  PendingOperation? get pendingOp => _pendingOp;
  LoadingState get loadingState => _loadingState;
  String? get errorMessage => _errorMessage;
  bool get configured => _configured;

  /// Expose the shared ApiClient so PowerProvider can reuse it.
  ApiClient? get client => _client;

  /// Reads credentials from Keychain and starts polling if configured.
  Future<void> initialize() async {
    final storage = CredentialStorage();
    final baseUrl = await storage.getBaseUrl();
    final token = await storage.getToken();

    if (baseUrl == null || baseUrl.isEmpty || token == null || token.isEmpty) {
      _configured = false;
      _status = DeviceStatus.unknown();
      notifyListeners();
      return;
    }

    _configured = true;
    _client = ApiClient(baseUrl, token);
    await refresh();
    _refreshTimer = Timer.periodic(AppDefaults.pollInterval, (_) => refresh());
  }

  Future<void> refresh() async {
    if (_client == null) return;
    _loadingState = LoadingState.loading;
    notifyListeners();
    try {
      final statusData = await _client!.deviceStatus();
      _status = DeviceStatus.fromJson(statusData);

      final capsData = await _client!.capabilities();
      _caps = Capabilities.fromJson(capsData);

      final pendingData = await _client!.pendingOperation();
      final pendingRaw = pendingData['data'];
      _pendingOp = pendingRaw != null
          ? PendingOperation.fromJson(pendingRaw as Map<String, dynamic>)
          : null;

      _loadingState = LoadingState.loaded;
      _errorMessage = null;
    } catch (e) {
      _loadingState = LoadingState.error;
      _status = DeviceStatus.offline();
      _errorMessage = _safeError(e);
    }
    notifyListeners();
  }

  /// Call after saving new credentials in settings to reload everything.
  Future<void> reconfigure() async {
    _refreshTimer?.cancel();
    _refreshTimer = null;
    _client = null;
    _configured = false;
    _status = DeviceStatus.unknown();
    notifyListeners();
    await initialize();
  }

  /// Sanitises errors — token strings are never surfaced.
  String _safeError(Object e) {
    final s = e.toString();
    if (s.contains('401')) return 'Authentication failed — check your token';
    if (s.contains('403')) return 'Access denied';
    if (s.contains('SocketException') || s.contains('Connection')) {
      return 'Cannot reach device';
    }
    return 'Connection error';
  }

  @override
  void dispose() {
    _refreshTimer?.cancel();
    super.dispose();
  }
}
