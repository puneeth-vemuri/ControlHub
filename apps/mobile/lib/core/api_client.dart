import 'package:dio/dio.dart';
import 'constants.dart';

/// Dio-based HTTP client. The auth token is placed in headers only —
/// it is NEVER written to logs, error messages, or UI text.
class ApiClient {
  late final Dio _dio;

  ApiClient(String baseUrl, String token) {
    _dio = Dio(
      BaseOptions(
        baseUrl: baseUrl,
        connectTimeout: AppDefaults.connectTimeout,
        receiveTimeout: AppDefaults.receiveTimeout,
        headers: {
          'Authorization': 'Bearer $token',
          'Content-Type': 'application/json',
        },
      ),
    );
    // Log only method + path + status — never headers, body, or token.
    _dio.interceptors.add(
      LogInterceptor(
        requestHeader: false,
        requestBody: false,
        responseHeader: false,
        responseBody: false,
      ),
    );
  }

  Future<Map<String, dynamic>> health() async {
    final r = await _dio.get(ApiEndpoints.health);
    return r.data as Map<String, dynamic>;
  }

  Future<Map<String, dynamic>> deviceStatus() async {
    final r = await _dio.get(ApiEndpoints.deviceStatus);
    return r.data as Map<String, dynamic>;
  }

  Future<Map<String, dynamic>> capabilities() async {
    final r = await _dio.get(ApiEndpoints.capabilities);
    return r.data as Map<String, dynamic>;
  }

  Future<Map<String, dynamic>> powerAction(
    String action, {
    bool confirm = false,
    int delaySeconds = AppDefaults.powerDelaySeconds,
    String reason = 'User initiated',
  }) async {
    final r = await _dio.post('/api/v1/power/$action', data: {
      'confirm': confirm,
      'delay_seconds': delaySeconds,
      'reason': reason,
    });
    return r.data as Map<String, dynamic>;
  }

  Future<Map<String, dynamic>> pendingOperation() async {
    final r = await _dio.get(ApiEndpoints.powerPending);
    return r.data as Map<String, dynamic>;
  }

  Future<Map<String, dynamic>> cancelOperation() async {
    final r = await _dio.post(ApiEndpoints.powerCancel);
    return r.data as Map<String, dynamic>;
  }
}
