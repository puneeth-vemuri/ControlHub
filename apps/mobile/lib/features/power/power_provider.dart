import 'package:flutter/foundation.dart';
import '../../core/api_client.dart';

/// Handles power actions and pending-cancel calls.
/// Receives an optional [ApiClient] shared from [DashboardProvider].
class PowerProvider extends ChangeNotifier {
  ApiClient? _client;
  bool _loading = false;
  String? _lastError;

  bool get loading => _loading;
  String? get lastError => _lastError;

  /// Update the client whenever [DashboardProvider] is reconfigured.
  void updateClient(ApiClient? client) {
    _client = client;
  }

  Future<bool> performAction(
    String action, {
    int delaySeconds = 30,
    String reason = 'User initiated',
  }) async {
    if (_client == null) {
      _lastError = 'Not connected — configure the app in Settings first';
      notifyListeners();
      return false;
    }
    _loading = true;
    _lastError = null;
    notifyListeners();
    try {
      await _client!.powerAction(
        action,
        confirm: true,
        delaySeconds: delaySeconds,
        reason: reason,
      );
      _loading = false;
      notifyListeners();
      return true;
    } catch (e) {
      _loading = false;
      _lastError = _safeError(e);
      notifyListeners();
      return false;
    }
  }

  Future<bool> cancelPending() async {
    if (_client == null) return false;
    try {
      await _client!.cancelOperation();
      return true;
    } catch (e) {
      _lastError = _safeError(e);
      notifyListeners();
      return false;
    }
  }

  /// Never exposes token or raw HTTP internals in error strings.
  String _safeError(Object e) {
    final s = e.toString();
    if (s.contains('503')) return 'Companion unavailable — cannot lock session';
    if (s.contains('409')) return 'Operation not available';
    if (s.contains('501')) return 'Not supported on this platform';
    if (s.contains('401')) return 'Authentication failed';
    return 'Request failed';
  }
}
