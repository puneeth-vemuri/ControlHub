import 'package:flutter/cupertino.dart';
import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import '../../core/api_client.dart';
import '../../core/constants.dart';
import '../../core/secure_storage.dart';
import '../dashboard/dashboard_provider.dart';

class SettingsPage extends StatefulWidget {
  const SettingsPage({super.key});

  @override
  State<SettingsPage> createState() => _SettingsPageState();
}

class _SettingsPageState extends State<SettingsPage> {
  final _storage = CredentialStorage();
  final _urlController = TextEditingController();
  final _tokenController = TextEditingController();

  bool _obscureToken = true;
  bool _testing = false;
  String? _testResult;
  bool _testSuccess = false;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    final url = await _storage.getBaseUrl() ?? AppDefaults.baseUrl;
    final token = await _storage.getToken() ?? '';
    if (mounted) {
      setState(() {
        _urlController.text = url;
        _tokenController.text = token;
      });
    }
  }

  Future<void> _testConnection() async {
    setState(() {
      _testing = true;
      _testResult = null;
    });

    try {
      final client = ApiClient(_urlController.text.trim(), _tokenController.text.trim());
      final res = await client.health();
      if (mounted) {
        setState(() {
          _testSuccess = res['status'] == 'ok';
          _testResult = _testSuccess ? 'Connection successful!' : 'Unexpected response';
        });
      }
    } catch (e) {
      if (mounted) {
        setState(() {
          _testSuccess = false;
          _testResult = 'Failed: ${e.toString().split('\n').first}';
        });
      }
    } finally {
      if (mounted) {
        setState(() => _testing = false);
      }
    }
  }

  Future<void> _save() async {
    final url = _urlController.text.trim();
    final token = _tokenController.text.trim();

    await _storage.setBaseUrl(url);
    await _storage.setToken(token);

    if (mounted) {
      final dashboardProvider = context.read<DashboardProvider>();
      await dashboardProvider.reconfigure();
      if (!mounted) return;

      showCupertinoDialog(
        context: context,
        builder: (ctx) => CupertinoAlertDialog(
          title: const Text('Saved'),
          content: const Text('Settings updated and connection re-initialized.'),
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

  Future<void> _clear() async {
    await _storage.clear();
    _urlController.text = AppDefaults.baseUrl;
    _tokenController.clear();
    if (mounted) {
      final dashboardProvider = context.read<DashboardProvider>();
      await dashboardProvider.reconfigure();
      setState(() {
        _testResult = 'Credentials cleared.';
        _testSuccess = false;
      });
    }
  }

  @override
  void dispose() {
    _urlController.dispose();
    _tokenController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    const mint = Color(0xFF8CF0C5);
    const panel = Color(0xFF131922);
    const muted = Color(0xFF8E9BAB);

    return Scaffold(
      backgroundColor: const Color(0xFF0A0D12),
      appBar: AppBar(
        title: const Text('Settings'),
        backgroundColor: panel,
        elevation: 0,
      ),
      body: ListView(
        padding: const EdgeInsets.all(20),
        children: [
          const Text(
            'CONNECTION SETTINGS',
            style: TextStyle(color: muted, fontSize: 11, letterSpacing: 1.5, fontWeight: FontWeight.bold),
          ),
          const SizedBox(height: 12),
          Container(
            padding: const EdgeInsets.all(16),
            decoration: BoxDecoration(color: panel, borderRadius: BorderRadius.circular(16)),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                const Text('Agent Base URL', style: TextStyle(color: muted, fontSize: 13)),
                const SizedBox(height: 6),
                CupertinoTextField(
                  controller: _urlController,
                  style: const TextStyle(color: Colors.white),
                  placeholder: 'http://127.0.0.1:8765',
                  placeholderStyle: const TextStyle(color: Colors.white24),
                  padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
                  decoration: BoxDecoration(
                    color: Colors.white.withValues(alpha: 0.05),
                    borderRadius: BorderRadius.circular(8),
                  ),
                ),
                const SizedBox(height: 16),
                const Text('API Token', style: TextStyle(color: muted, fontSize: 13)),
                const SizedBox(height: 6),
                CupertinoTextField(
                  controller: _tokenController,
                  obscureText: _obscureToken,
                  style: const TextStyle(color: Colors.white),
                  placeholder: '32+ character Bearer token',
                  placeholderStyle: const TextStyle(color: Colors.white24),
                  padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
                  decoration: BoxDecoration(
                    color: Colors.white.withValues(alpha: 0.05),
                    borderRadius: BorderRadius.circular(8),
                  ),
                  suffix: CupertinoButton(
                    padding: const EdgeInsets.symmetric(horizontal: 8),
                    child: Icon(
                      _obscureToken ? CupertinoIcons.eye : CupertinoIcons.eye_slash,
                      color: muted,
                      size: 18,
                    ),
                    onPressed: () => setState(() => _obscureToken = !_obscureToken),
                  ),
                ),
              ],
            ),
          ),
          const SizedBox(height: 20),
          if (_testResult != null)
            Container(
              padding: const EdgeInsets.all(12),
              margin: const EdgeInsets.only(bottom: 16),
              decoration: BoxDecoration(
                color: _testSuccess ? mint.withValues(alpha: 0.15) : Colors.red.withValues(alpha: 0.15),
                borderRadius: BorderRadius.circular(10),
              ),
              child: Text(
                _testResult!,
                style: TextStyle(
                  color: _testSuccess ? mint : Colors.redAccent,
                  fontSize: 13,
                  fontWeight: FontWeight.w600,
                ),
              ),
            ),
          Row(
            children: [
              Expanded(
                child: CupertinoButton(
                  color: panel,
                  onPressed: _testing ? null : _testConnection,
                  child: _testing
                      ? const CupertinoActivityIndicator(color: mint)
                      : const Text('Test', style: TextStyle(color: mint, fontSize: 14)),
                ),
              ),
              const SizedBox(width: 12),
              Expanded(
                child: CupertinoButton(
                  color: mint,
                  onPressed: _save,
                  child: const Text('Save', style: TextStyle(color: Colors.black, fontWeight: FontWeight.bold, fontSize: 14)),
                ),
              ),
            ],
          ),
          const SizedBox(height: 12),
          CupertinoButton(
            onPressed: _clear,
            child: const Text('Clear Credentials', style: TextStyle(color: Colors.redAccent, fontSize: 13)),
          ),
        ],
      ),
    );
  }
}
