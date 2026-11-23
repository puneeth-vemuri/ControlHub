import 'package:flutter/cupertino.dart';
import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import 'features/dashboard/dashboard_page.dart';
import 'features/dashboard/dashboard_provider.dart';
import 'features/power/power_provider.dart';
import 'features/settings/settings_page.dart';

void main() async {
  WidgetsFlutterBinding.ensureInitialized();
  runApp(const ControlHubApp());
}

const ink = Color(0xFF0A0D12);
const panel = Color(0xFF131922);
const muted = Color(0xFF8E9BAB);
const mint = Color(0xFF8CF0C5);

class ControlHubApp extends StatelessWidget {
  const ControlHubApp({super.key});

  @override
  Widget build(BuildContext context) {
    return MultiProvider(
      providers: [
        ChangeNotifierProvider(
          create: (_) => DashboardProvider()..initialize(),
        ),
        ChangeNotifierProxyProvider<DashboardProvider, PowerProvider>(
          create: (_) => PowerProvider(),
          update: (_, dashboard, power) => (power ?? PowerProvider())..updateClient(dashboard.client),
        ),
      ],
      child: MaterialApp(
        title: 'ControlHub',
        debugShowCheckedModeBanner: false,
        theme: ThemeData.dark().copyWith(
          scaffoldBackgroundColor: ink,
          colorScheme: const ColorScheme.dark(primary: mint, surface: panel),
          navigationBarTheme: NavigationBarThemeData(
            backgroundColor: const Color(0xFF10151C),
            indicatorColor: mint.withValues(alpha: .13),
            labelTextStyle: WidgetStateProperty.resolveWith(
              (states) => TextStyle(
                color: states.contains(WidgetState.selected) ? mint : muted,
                fontSize: 11,
                fontWeight: FontWeight.w600,
              ),
            ),
          ),
        ),
        home: const HubShell(),
      ),
    );
  }
}

class HubShell extends StatefulWidget {
  const HubShell({super.key});

  @override
  State<HubShell> createState() => _HubShellState();
}

class _HubShellState extends State<HubShell> {
  int selected = 0;

  final pages = const [
    DashboardPage(),
    SimplePage(title: 'Remote', icon: CupertinoIcons.desktopcomputer),
    SimplePage(title: 'Files', icon: CupertinoIcons.folder),
    SimplePage(title: 'Activity', icon: CupertinoIcons.bell),
    SettingsPage(),
  ];

  @override
  Widget build(BuildContext context) => Scaffold(
        body: SafeArea(child: IndexedStack(index: selected, children: pages)),
        bottomNavigationBar: NavigationBar(
          height: 70,
          selectedIndex: selected,
          onDestinationSelected: (value) => setState(() => selected = value),
          destinations: const [
            NavigationDestination(icon: Icon(CupertinoIcons.house), label: 'Home'),
            NavigationDestination(icon: Icon(CupertinoIcons.desktopcomputer), label: 'Remote'),
            NavigationDestination(icon: Icon(CupertinoIcons.folder), label: 'Files'),
            NavigationDestination(icon: Icon(CupertinoIcons.bell), label: 'Activity'),
            NavigationDestination(icon: Icon(CupertinoIcons.gear), label: 'Settings'),
          ],
        ),
      );
}

class SimplePage extends StatelessWidget {
  const SimplePage({super.key, required this.title, required this.icon});
  final String title;
  final IconData icon;

  @override
  Widget build(BuildContext context) => Center(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Icon(icon, size: 40, color: mint),
            const SizedBox(height: 14),
            Text(title, style: Theme.of(context).textTheme.headlineSmall),
            const SizedBox(height: 6),
            const Text(
              'Available as the next milestone is implemented',
              style: TextStyle(color: muted, fontSize: 12),
            ),
          ],
        ),
      );
}
