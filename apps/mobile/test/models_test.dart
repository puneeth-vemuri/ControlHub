import 'package:flutter_test/flutter_test.dart';
import 'package:controlhub_mobile/domain/models.dart';

void main() {
  group('DeviceStatus', () {
    test('fromJson parses correctly', () {
      final status = DeviceStatus.fromJson({
        'host': 'my-laptop',
        'os': 'Windows 11',
        'agent': 'online',
        'companion': 'online',
        'uptime_seconds': 3665,
      });

      expect(status.host, 'my-laptop');
      expect(status.os, 'Windows 11');
      expect(status.agent, 'online');
      expect(status.companion, 'online');
      expect(status.uptimeSeconds, 3665);
      expect(status.connectionState, DeviceConnectionState.online);
      expect(status.uptimeFormatted, '1h 1m');
    });

    test('offline() returns expected state', () {
      final status = DeviceStatus.offline();
      expect(status.connectionState, DeviceConnectionState.offline);
      expect(status.agent, 'offline');
    });

    test('unknown() returns expected state', () {
      final status = DeviceStatus.unknown();
      expect(status.connectionState, DeviceConnectionState.unknown);
      expect(status.host, 'Not configured');
    });
  });

  group('Capabilities', () {
    test('fromJson parses capabilities correctly', () {
      final caps = Capabilities.fromJson({
        'power': {
          'shutdown': true,
          'restart': true,
          'sleep': true,
          'hibernate': false,
          'lock': true,
          'cancel': true,
        },
        'companion': true,
      });

      expect(caps.shutdown, isTrue);
      expect(caps.restart, isTrue);
      expect(caps.sleep, isTrue);
      expect(caps.hibernate, isFalse);
      expect(caps.lock, isTrue);
      expect(caps.cancel, isTrue);
      expect(caps.companion, isTrue);
    });

    test('empty json defaults to all false', () {
      final caps = Capabilities.fromJson({});
      expect(caps.shutdown, isFalse);
      expect(caps.restart, isFalse);
      expect(caps.lock, isFalse);
    });
  });

  group('PendingOperation', () {
    test('fromJson parses operation correctly', () {
      final op = PendingOperation.fromJson({
        'operation_id': 'op-123',
        'action': 'shutdown',
        'scheduled_for': '2026-11-15T22:00:00Z',
        'cancellable': true,
        'state': 'accepted',
      });

      expect(op.operationId, 'op-123');
      expect(op.action, 'shutdown');
      expect(op.cancellable, isTrue);
      expect(op.state, 'accepted');
    });
  });
}
