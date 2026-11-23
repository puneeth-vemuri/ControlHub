import 'package:flutter/cupertino.dart';

Future<bool> showPowerConfirmation(
  BuildContext context, {
  required String action,
  required String description,
}) async {
  final actionTitle = action.isNotEmpty
      ? '${action[0].toUpperCase()}${action.substring(1)}'
      : action;

  final result = await showCupertinoDialog<bool>(
    context: context,
    builder: (ctx) => CupertinoAlertDialog(
      title: Text('Confirm $actionTitle'),
      content: Padding(
        padding: const EdgeInsets.only(top: 8),
        child: Text(description),
      ),
      actions: [
        CupertinoDialogAction(
          isDestructiveAction: action == 'shutdown' || action == 'restart',
          onPressed: () => Navigator.of(ctx).pop(true),
          child: Text(actionTitle),
        ),
        CupertinoDialogAction(
          isDefaultAction: true,
          onPressed: () => Navigator.of(ctx).pop(false),
          child: const Text('Cancel'),
        ),
      ],
    ),
  );
  return result ?? false;
}
