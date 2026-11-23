import 'package:flutter_secure_storage/flutter_secure_storage.dart';

/// Wraps flutter_secure_storage to persist the API base URL and bearer token
/// in the iOS Keychain. Values are NEVER logged or exposed in UI strings.
class CredentialStorage {
  static const _storage = FlutterSecureStorage(
    iOptions: IOSOptions(
      accessibility: KeychainAccessibility.first_unlock_this_device,
    ),
  );

  static const _baseUrlKey = 'controlhub_base_url';
  static const _tokenKey = 'controlhub_token';

  Future<String?> getBaseUrl() => _storage.read(key: _baseUrlKey);
  Future<String?> getToken() => _storage.read(key: _tokenKey);

  Future<void> setBaseUrl(String url) =>
      _storage.write(key: _baseUrlKey, value: url);

  Future<void> setToken(String token) =>
      _storage.write(key: _tokenKey, value: token);

  Future<void> clear() async {
    await _storage.delete(key: _baseUrlKey);
    await _storage.delete(key: _tokenKey);
  }
}
