import 'dart:convert';

import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';
import 'package:http/http.dart' as http;
import 'package:shared_preferences/shared_preferences.dart';
import 'package:url_launcher/url_launcher.dart';

void main() {
  runApp(const TaccuinoApp());
}

class TaccuinoApp extends StatelessWidget {
  const TaccuinoApp({super.key});

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'Taccuino Ristoranti',
      theme: ThemeData(
        colorScheme: ColorScheme.fromSeed(seedColor: const Color(0xFF8F2F18)),
        scaffoldBackgroundColor: const Color(0xFFF7EEE5),
        useMaterial3: true,
      ),
      home: const HomePage(),
    );
  }
}

class HomePage extends StatefulWidget {
  const HomePage({super.key});

  @override
  State<HomePage> createState() => _HomePageState();
}

class _HomePageState extends State<HomePage> {
  static const _apiBaseUrlFromEnv = String.fromEnvironment('API_BASE_URL');
  static const _brandLogoAsset = 'assets/logo-4ristoranti.jpg';
  static const _categoryLabels = {
    'location': 'Location',
    'menu': 'Menu',
    'service': 'Servizio',
    'bill': 'Conto',
    'special': 'Special',
  };

  static String _resolveBaseUrl() {
    if (_apiBaseUrlFromEnv.isNotEmpty) {
      return _apiBaseUrlFromEnv;
    }

    if (kIsWeb) {
      return 'http://localhost:5285';
    }

    return defaultTargetPlatform == TargetPlatform.android
        ? 'http://10.0.2.2:5285'
        : 'http://localhost:5285';
  }

  static final baseUrl = _resolveBaseUrl();
  static const tokenKey = 'tr_token';
  static const refreshTokenKey = 'tr_refresh_token';
  static const accessExpiresAtKey = 'tr_access_expires_at';
  static const refreshExpiresAtKey = 'tr_refresh_expires_at';

  final emailController = TextEditingController();
  final passwordController = TextEditingController();
  final forgotEmailController = TextEditingController();
  final resetTokenController = TextEditingController();
  final newPasswordController = TextEditingController();
  final confirmNewPasswordController = TextEditingController();
  final nameController = TextEditingController();
  final streetController = TextEditingController();
  final cityController = TextEditingController();
  final notesController = TextEditingController();
  final joinCodeController = TextEditingController();

  AuthView authView = AuthView.login;
  bool busy = false;
  String? token;
  String? refreshToken;
  DateTime? accessExpiresAt;
  DateTime? refreshExpiresAt;
  String error = '';
  String info = '';
  List<dynamic> meals = [];
  Map<String, int> rating = {
    'location': 0,
    'menu': 0,
    'service': 0,
    'bill': 0,
    'special': 0,
  };
  Map<String, dynamic>? selectedMeal;

  @override
  void initState() {
    super.initState();
    _initResetTokenFromUrl();
    _loadSession();
  }

  @override
  void dispose() {
    emailController.dispose();
    passwordController.dispose();
    forgotEmailController.dispose();
    resetTokenController.dispose();
    newPasswordController.dispose();
    confirmNewPasswordController.dispose();
    nameController.dispose();
    streetController.dispose();
    cityController.dispose();
    notesController.dispose();
    joinCodeController.dispose();
    super.dispose();
  }

  void _initResetTokenFromUrl() {
    final tokenFromQuery = Uri.base.queryParameters['token'];
    if (tokenFromQuery != null && tokenFromQuery.isNotEmpty) {
      resetTokenController.text = tokenFromQuery;
      authView = AuthView.resetPassword;
    }
  }

  bool _isExpired(DateTime? dt) {
    if (dt == null) {
      return true;
    }

    return !dt.isAfter(DateTime.now().toUtc());
  }

  Future<void> _loadSession() async {
    final prefs = await SharedPreferences.getInstance();
    final savedToken = prefs.getString(tokenKey);
    final savedRefreshToken = prefs.getString(refreshTokenKey);
    final savedAccessExpiry = prefs.getString(accessExpiresAtKey);
    final savedRefreshExpiry = prefs.getString(refreshExpiresAtKey);

    if (savedToken != null &&
        savedRefreshToken != null &&
        savedAccessExpiry != null &&
        savedRefreshExpiry != null) {
      setState(() {
        token = savedToken;
        refreshToken = savedRefreshToken;
        accessExpiresAt = DateTime.tryParse(savedAccessExpiry)?.toUtc();
        refreshExpiresAt = DateTime.tryParse(savedRefreshExpiry)?.toUtc();
      });

      final valid = await _ensureAccessToken();
      if (valid) {
        await _loadMeals();
      }
    }
  }

  Future<void> _saveSession({
    required String accessToken,
    required String newRefreshToken,
    required DateTime accessExpiryUtc,
    required DateTime refreshExpiryUtc,
  }) async {
    final prefs = await SharedPreferences.getInstance();
    await prefs.setString(tokenKey, accessToken);
    await prefs.setString(refreshTokenKey, newRefreshToken);
    await prefs.setString(
      accessExpiresAtKey,
      accessExpiryUtc.toUtc().toIso8601String(),
    );
    await prefs.setString(
      refreshExpiresAtKey,
      refreshExpiryUtc.toUtc().toIso8601String(),
    );

    setState(() {
      token = accessToken;
      refreshToken = newRefreshToken;
      accessExpiresAt = accessExpiryUtc.toUtc();
      refreshExpiresAt = refreshExpiryUtc.toUtc();
    });
  }

  Future<void> _clearSession() async {
    final prefs = await SharedPreferences.getInstance();
    await prefs.remove(tokenKey);
    await prefs.remove(refreshTokenKey);
    await prefs.remove(accessExpiresAtKey);
    await prefs.remove(refreshExpiresAtKey);

    setState(() {
      token = null;
      refreshToken = null;
      accessExpiresAt = null;
      refreshExpiresAt = null;
      meals = [];
      selectedMeal = null;
    });
  }

  Future<bool> _refreshSession() async {
    final currentRefreshToken = refreshToken;
    if (currentRefreshToken == null || _isExpired(refreshExpiresAt)) {
      await _clearSession();
      return false;
    }

    final response = await http.post(
      Uri.parse('$baseUrl/api/auth/refresh'),
      headers: {'Content-Type': 'application/json'},
      body: jsonEncode({'refreshToken': currentRefreshToken}),
    );

    if (response.statusCode >= 200 && response.statusCode < 300) {
      final payload = jsonDecode(response.body) as Map<String, dynamic>;
      await _saveSession(
        accessToken: payload['token'] as String,
        newRefreshToken: payload['refreshToken'] as String,
        accessExpiryUtc: DateTime.parse(
          payload['accessTokenExpiresAtUtc'] as String,
        ),
        refreshExpiryUtc: DateTime.parse(
          payload['refreshTokenExpiresAtUtc'] as String,
        ),
      );
      return true;
    }

    await _clearSession();
    return false;
  }

  Future<bool> _ensureAccessToken() async {
    if (token == null) {
      return false;
    }

    if (!_isExpired(accessExpiresAt)) {
      return true;
    }

    return _refreshSession();
  }

  Future<Map<String, String>?> _getAuthHeaders() async {
    final valid = await _ensureAccessToken();
    if (!valid || token == null) {
      return null;
    }

    return {
      'Content-Type': 'application/json',
      'Authorization': 'Bearer $token',
    };
  }

  Future<http.Response?> _sendAuthorized(
    Future<http.Response> Function(Map<String, String> headers) send,
  ) async {
    var headers = await _getAuthHeaders();
    if (headers == null) {
      return null;
    }

    var response = await send(headers);
    if (response.statusCode != 401) {
      return response;
    }

    final refreshed = await _refreshSession();
    if (!refreshed) {
      return response;
    }

    headers = await _getAuthHeaders();
    if (headers == null) {
      return response;
    }

    return send(headers);
  }

  Future<void> _authenticate() async {
    setState(() {
      busy = true;
      error = '';
      info = '';
    });

    final path = authView == AuthView.login
        ? '/api/auth/login'
        : '/api/auth/register';
    final response = await http.post(
      Uri.parse('$baseUrl$path'),
      headers: {'Content-Type': 'application/json'},
      body: jsonEncode({
        'email': emailController.text.trim(),
        'password': passwordController.text,
      }),
    );

    if (response.statusCode >= 200 && response.statusCode < 300) {
      final payload = jsonDecode(response.body) as Map<String, dynamic>;
      await _saveSession(
        accessToken: payload['token'] as String,
        newRefreshToken: payload['refreshToken'] as String,
        accessExpiryUtc: DateTime.parse(
          payload['accessTokenExpiresAtUtc'] as String,
        ),
        refreshExpiryUtc: DateTime.parse(
          payload['refreshTokenExpiresAtUtc'] as String,
        ),
      );
      await _loadMeals();
    } else {
      setState(() {
        error = _extractErrorMessage(
          response,
          fallback: 'Autenticazione fallita',
        );
      });
    }

    setState(() {
      busy = false;
    });
  }

  Future<void> _requestPasswordReset() async {
    setState(() {
      busy = true;
      error = '';
      info = '';
    });

    final response = await http.post(
      Uri.parse('$baseUrl/api/auth/forgot-password'),
      headers: {'Content-Type': 'application/json'},
      body: jsonEncode({'email': forgotEmailController.text.trim()}),
    );

    if (response.statusCode >= 200 && response.statusCode < 300) {
      final data = jsonDecode(response.body) as Map<String, dynamic>;
      setState(() {
        info = (data['message'] as String?) ??
            'Se l\'email e registrata, riceverai un link di recupero.';
        authView = AuthView.login;
      });
    } else {
      setState(() {
        error = _extractErrorMessage(
          response,
          fallback: 'Richiesta recupero password non riuscita',
        );
      });
    }

    setState(() {
      busy = false;
    });
  }

  Future<void> _resetPassword() async {
    final newPassword = newPasswordController.text;
    final confirmPassword = confirmNewPasswordController.text;

    if (newPassword != confirmPassword) {
      setState(() {
        error = 'Le password non coincidono';
      });
      return;
    }

    setState(() {
      busy = true;
      error = '';
      info = '';
    });

    final response = await http.post(
      Uri.parse('$baseUrl/api/auth/reset-password'),
      headers: {'Content-Type': 'application/json'},
      body: jsonEncode({
        'token': resetTokenController.text.trim(),
        'newPassword': newPassword,
      }),
    );

    if (response.statusCode >= 200 && response.statusCode < 300) {
      final data = jsonDecode(response.body) as Map<String, dynamic>;
      setState(() {
        info = (data['message'] as String?) ??
            'Password aggiornata correttamente.';
        authView = AuthView.login;
        resetTokenController.clear();
        newPasswordController.clear();
        confirmNewPasswordController.clear();
      });
    } else {
      setState(() {
        error = _extractErrorMessage(
          response,
          fallback: 'Cambio password non riuscito',
        );
      });
    }

    setState(() {
      busy = false;
    });
  }

  String _extractErrorMessage(http.Response response, {required String fallback}) {
    try {
      final payload = jsonDecode(response.body);
      if (payload is String && payload.isNotEmpty) {
        return payload;
      }

      if (payload is Map<String, dynamic>) {
        final message = payload['message'];
        if (message is String && message.isNotEmpty) {
          return message;
        }

        if (payload.isNotEmpty) {
          return payload.values.map((v) => v.toString()).join(' ');
        }
      }

      if (payload is List) {
        return payload.map((v) => v.toString()).join(' ');
      }
    } catch (_) {
      // Ignore parsing issues and fallback.
    }

    return fallback;
  }

  Future<void> _logout() async {
    if (refreshToken != null) {
      await _sendAuthorized((headers) {
        return http.post(
          Uri.parse('$baseUrl/api/auth/logout'),
          headers: headers,
          body: jsonEncode({'refreshToken': refreshToken}),
        );
      });
    }

    await _clearSession();
  }

  Future<void> _loadMeals() async {
    final response = await _sendAuthorized((headers) {
      return http.get(Uri.parse('$baseUrl/api/meals'), headers: headers);
    });

    if (response == null) {
      return;
    }

    if (response.statusCode >= 200 && response.statusCode < 300) {
      final payload = jsonDecode(response.body) as List<dynamic>;
      setState(() {
        meals = payload;
        if (selectedMeal != null) {
          final selectedId = selectedMeal?['id']?.toString();
          final updatedSelection = payload
              .whereType<Map<String, dynamic>>()
              .firstWhere(
                (meal) => meal['id']?.toString() == selectedId,
                orElse: () => selectedMeal!,
              );
          selectedMeal = updatedSelection;
          rating = _ratingFromMeal(updatedSelection);
        }
      });
    } else {
      setState(() {
        error = 'Impossibile caricare i pasti';
      });
    }
  }

  Future<void> _createMeal() async {
    setState(() {
      busy = true;
      error = '';
    });

    final response = await _sendAuthorized((headers) {
      return http.post(
        Uri.parse('$baseUrl/api/meals'),
        headers: headers,
        body: jsonEncode({
          'name': nameController.text,
          'street': streetController.text,
          'city': cityController.text,
          'notes': notesController.text,
          'mealType': 0,
        }),
      );
    });

    if (response == null) {
      setState(() => busy = false);
      return;
    }

    if (response.statusCode >= 200 && response.statusCode < 300) {
      final payload = jsonDecode(response.body) as Map<String, dynamic>;
      setState(() {
        selectedMeal = payload;
        rating = _ratingFromMeal(payload);
      });
      await _loadMeals();
    } else {
      setState(() {
        error = 'Creazione pasto fallita';
      });
    }

    setState(() {
      busy = false;
    });
  }

  Future<void> _joinByCode() async {
    setState(() {
      busy = true;
      error = '';
    });

    final response = await _sendAuthorized((headers) {
      return http.post(
        Uri.parse('$baseUrl/api/meals/join'),
        headers: headers,
        body: jsonEncode({
          'shareCode': joinCodeController.text.trim().toUpperCase(),
        }),
      );
    });

    if (response == null) {
      setState(() => busy = false);
      return;
    }

    if (response.statusCode >= 200 && response.statusCode < 300) {
      final payload = jsonDecode(response.body) as Map<String, dynamic>;
      setState(() {
        selectedMeal = payload;
        rating = _ratingFromMeal(payload);
      });
      await _loadMeals();
    } else {
      setState(() {
        error = 'Codice non valido';
      });
    }

    setState(() {
      busy = false;
    });
  }

  Future<void> _submitRating() async {
    final mealId = selectedMeal?['id'];
    if (mealId == null) {
      return;
    }

    setState(() {
      busy = true;
      error = '';
    });

    final response = await _sendAuthorized((headers) {
      return http.post(
        Uri.parse('$baseUrl/api/meals/$mealId/ratings'),
        headers: headers,
        body: jsonEncode(rating),
      );
    });

    if (response == null) {
      setState(() => busy = false);
      return;
    }

    if (response.statusCode >= 200 && response.statusCode < 300) {
      final payload = jsonDecode(response.body) as Map<String, dynamic>;
      setState(() {
        selectedMeal = payload;
        rating = _ratingFromMeal(payload);
      });
      await _loadMeals();
    } else {
      setState(() {
        error = 'Voto non accettato (dati errati)';
      });
    }

    setState(() {
      busy = false;
    });
  }

  Map<String, int> _ratingFromMeal(Map<String, dynamic>? meal) {
    final userRating = meal?['userRating'];
    if (userRating is! Map<String, dynamic>) {
      return {'location': 0, 'menu': 0, 'service': 0, 'bill': 0, 'special': 0};
    }

    return {
      'location': (userRating['location'] as num?)?.toInt() ?? 0,
      'menu': (userRating['menu'] as num?)?.toInt() ?? 0,
      'service': (userRating['service'] as num?)?.toInt() ?? 0,
      'bill': (userRating['bill'] as num?)?.toInt() ?? 0,
      'special': (userRating['special'] as num?)?.toInt() ?? 0,
    };
  }

  Future<void> _shareSelectedMealOnWhatsApp() async {
    final meal = selectedMeal;
    if (meal == null) {
      return;
    }

    final code = meal['shareCode']?.toString() ?? '';
    final name = meal['name']?.toString() ?? 'Taccuino 4 Ristoranti';
    final text = Uri.encodeComponent(
      'Partecipa a $name su Taccuino 4 Ristoranti. Codice: $code.',
    );
    final uri = Uri.parse('https://wa.me/?text=$text');

    final launched = await launchUrl(uri, mode: LaunchMode.externalApplication);
    if (!launched) {
      setState(() {
        error = 'Impossibile aprire WhatsApp.';
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    final sessionText = accessExpiresAt == null
        ? 'Sessione non attiva'
        : 'Sessione valida fino alle ${TimeOfDay.fromDateTime(accessExpiresAt!.toLocal()).format(context)}';

    return Scaffold(
      appBar: AppBar(
        title: const Text('Taccuino 4 Ristoranti'),
        actions: token != null
            ? [
                Padding(
                  padding: const EdgeInsets.only(right: 8),
                  child: TextButton.icon(
                    onPressed: _logout,
                    style: TextButton.styleFrom(
                      backgroundColor: const Color(0xFF8F2F18),
                      foregroundColor: Colors.white,
                      padding: const EdgeInsets.symmetric(horizontal: 12),
                    ),
                    icon: const Icon(Icons.logout),
                    label: const Text('Logout'),
                  ),
                ),
              ]
            : null,
      ),
      body: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          children: [
            _buildBrandHeader(),
            const SizedBox(height: 12),
            if (token != null)
              Align(
                alignment: Alignment.centerLeft,
                child: Text(sessionText, style: const TextStyle(fontSize: 12)),
              ),
            if (token != null) const SizedBox(height: 8),
            Expanded(child: token == null ? _buildAuth() : _buildMain()),
          ],
        ),
      ),
    );
  }

  Widget _buildBrandHeader() {
    return ClipRRect(
      borderRadius: BorderRadius.circular(16),
      child: Stack(
        alignment: Alignment.bottomLeft,
        children: [
          Image.asset(
            _brandLogoAsset,
            height: 150,
            width: double.infinity,
            fit: BoxFit.cover,
          ),
          Container(
            width: double.infinity,
            padding: const EdgeInsets.all(10),
            decoration: const BoxDecoration(
              gradient: LinearGradient(
                begin: Alignment.bottomCenter,
                end: Alignment.topCenter,
                colors: [Color(0xCC2A1008), Color(0x002A1008)],
              ),
            ),
            child: const Text(
              'Assegna il tuo voto e ribalta il risultato!',
              style: TextStyle(
                color: Colors.white,
                fontWeight: FontWeight.w600,
              ),
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildAuth() {
    final isLogin = authView == AuthView.login;
    final isRegister = authView == AuthView.register;
    final isForgot = authView == AuthView.forgotPassword;
    final isReset = authView == AuthView.resetPassword;

    return ListView(
      children: [
        Text(
          isLogin
              ? 'Login'
              : isRegister
                  ? 'Registrazione'
                  : isForgot
                      ? 'Recupera password'
                      : 'Imposta nuova password',
          style: Theme.of(context).textTheme.headlineMedium,
        ),
        const SizedBox(height: 12),
        if (isLogin || isRegister) ...[
          TextField(
            controller: emailController,
            decoration: const InputDecoration(labelText: 'Email'),
          ),
          const SizedBox(height: 8),
          TextField(
            controller: passwordController,
            obscureText: true,
            decoration: const InputDecoration(labelText: 'Password'),
          ),
          const SizedBox(height: 12),
          FilledButton(
            onPressed: busy ? null : _authenticate,
            child: Text(isLogin ? 'Accedi' : 'Registrati'),
          ),
        ],
        if (isForgot) ...[
          TextField(
            controller: forgotEmailController,
            decoration: const InputDecoration(labelText: 'Email registrata'),
          ),
          const SizedBox(height: 12),
          FilledButton(
            onPressed: busy ? null : _requestPasswordReset,
            child: const Text('Invia link di recupero'),
          ),
        ],
        if (isReset) ...[
          TextField(
            controller: resetTokenController,
            decoration: const InputDecoration(labelText: 'Token di reset'),
          ),
          const SizedBox(height: 8),
          TextField(
            controller: newPasswordController,
            obscureText: true,
            decoration: const InputDecoration(labelText: 'Nuova password'),
          ),
          const SizedBox(height: 8),
          TextField(
            controller: confirmNewPasswordController,
            obscureText: true,
            decoration:
                const InputDecoration(labelText: 'Conferma nuova password'),
          ),
          const SizedBox(height: 12),
          FilledButton(
            onPressed: busy ? null : _resetPassword,
            child: const Text('Cambia password'),
          ),
        ],
        if (!isLogin)
          TextButton(
            onPressed: () => setState(() => authView = AuthView.login),
            child: const Text('Torna al login'),
          ),
        if (isLogin)
          TextButton(
            onPressed: () => setState(() => authView = AuthView.register),
            child: const Text('Crea account'),
          ),
        if (isRegister)
          TextButton(
            onPressed: () => setState(() => authView = AuthView.login),
            child: const Text('Ho gia un account'),
          ),
        if (isLogin)
          TextButton(
            onPressed: () => setState(() => authView = AuthView.forgotPassword),
            child: const Text('Hai dimenticato la password?'),
          ),
        if (isForgot)
          TextButton(
            onPressed: () => setState(() => authView = AuthView.resetPassword),
            child: const Text('Hai gia il token? Cambia password'),
          ),
        if (info.isNotEmpty)
          Text(info, style: const TextStyle(color: Color(0xFF1D6F35))),
        if (error.isNotEmpty)
          Text(error, style: const TextStyle(color: Colors.red)),
      ],
    );
  }

  Widget _buildMain() {
    final summary = selectedMeal?['summary'] as Map<String, dynamic>?;
    final leaderboard = selectedMeal?['leaderboard'] as List<dynamic>?;
    final userRating = selectedMeal?['userRating'] as Map<String, dynamic>?;

    return ListView(
      children: [
        Text('Crea pasto', style: Theme.of(context).textTheme.titleLarge),
        TextField(
          controller: nameController,
          decoration: const InputDecoration(labelText: 'Nome locale'),
        ),
        TextField(
          controller: streetController,
          decoration: const InputDecoration(labelText: 'Via'),
        ),
        TextField(
          controller: cityController,
          decoration: const InputDecoration(labelText: 'Citta'),
        ),
        TextField(
          controller: notesController,
          decoration: const InputDecoration(labelText: 'Note'),
        ),
        const SizedBox(height: 8),
        FilledButton(
          onPressed: busy ? null : _createMeal,
          child: const Text('Crea pasto'),
        ),
        const Divider(height: 24),
        TextField(
          controller: joinCodeController,
          textCapitalization: TextCapitalization.characters,
          decoration: const InputDecoration(labelText: 'Codice condivisione'),
        ),
        const SizedBox(height: 8),
        FilledButton(
          onPressed: busy ? null : _joinByCode,
          child: const Text('Apri pasto'),
        ),
        const Divider(height: 24),
        Text('I tuoi pasti', style: Theme.of(context).textTheme.titleLarge),
        ...meals.map((meal) {
          final map = meal as Map<String, dynamic>;
          return ListTile(
            title: Text(map['name']?.toString() ?? '-'),
            subtitle: Text(
              'Codice: ${map['shareCode']} | Media: ${map['summary']?['overall'] ?? 0}',
            ),
            onTap: () {
              setState(() {
                selectedMeal = map;
                rating = _ratingFromMeal(map);
              });
            },
          );
        }),
        const Divider(height: 24),
        if (selectedMeal != null) ...[
          Text('Pasto selezionato: ${selectedMeal!['name']}'),
          Text('Codice: ${selectedMeal!['shareCode']}'),
          const SizedBox(height: 8),
          OutlinedButton.icon(
            onPressed: _shareSelectedMealOnWhatsApp,
            style: OutlinedButton.styleFrom(
              backgroundColor: const Color(0xFF3A5536),
              foregroundColor: Colors.white,
              side: const BorderSide(color: Color(0xFF3A5536)),
              padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 14),
            ),
            icon: const Icon(Icons.share),
            label: const Text('Condividi codice su WhatsApp'),
          ),
          if (summary != null)
            Text(
              'Voti: ${summary['votes']} - Location ${summary['location']} Menu ${summary['menu']} Servizio ${summary['service']} Conto ${summary['bill']} Special ${summary['special']}',
            ),
          const SizedBox(height: 8),
          Card(
            child: Padding(
              padding: const EdgeInsets.all(12),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    'La tua scheda voto',
                    style: Theme.of(context).textTheme.titleMedium,
                  ),
                  const SizedBox(height: 6),
                  Text(
                    userRating == null
                        ? 'Non hai ancora votato questo pasto.'
                        : 'Ultimo voto salvato.',
                  ),
                  const SizedBox(height: 8),
                  _buildRatingGrid(),
                ],
              ),
            ),
          ),
          const SizedBox(height: 8),
          ExpansionTile(
            title: Text(
              'Scopri classifica finale (${summary?['votes'] ?? 0} votanti)',
            ),
            children: [
              if (leaderboard != null && leaderboard.isNotEmpty)
                ...leaderboard.map((entry) {
                  final p = entry as Map<String, dynamic>;
                  return ListTile(
                    leading: CircleAvatar(child: Text('${p['position']}')),
                    title: Text(p['userEmail']?.toString() ?? 'utente'),
                    subtitle: Text(
                      'Location ${p['location']}  Menu ${p['menu']}  Servizio ${p['service']}  Conto ${p['bill']}  Special ${p['special']}',
                    ),
                    trailing: Text((p['overall'] ?? 0).toString()),
                  );
                }),
            ],
          ),
          const SizedBox(height: 8),
          ...rating.keys.map((field) {
            return Padding(
              padding: const EdgeInsets.only(bottom: 8),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text('${_categoryLabels[field] ?? field}: ${rating[field]}'),
                  Slider(
                    value: (rating[field] ?? 0).toDouble(),
                    min: 0,
                    max: 10,
                    divisions: 10,
                    label: (rating[field] ?? 0).toString(),
                    onChanged: (value) {
                      setState(() {
                        rating[field] = value.round();
                      });
                    },
                  ),
                ],
              ),
            );
          }),
          FilledButton(
            onPressed: busy ? null : _submitRating,
            child: Text(
              (selectedMeal?['hasUserRated'] as bool? ?? false)
                  ? 'Aggiorna voto'
                  : 'Invia voto',
            ),
          ),
        ],
        if (error.isNotEmpty)
          Text(error, style: const TextStyle(color: Colors.red)),
      ],
    );
  }

  Widget _buildRatingGrid() {
    return Wrap(
      spacing: 8,
      runSpacing: 8,
      children: rating.entries.map((entry) {
        final label = _categoryLabels[entry.key] ?? entry.key;

        return Container(
          width: 110,
          padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 6),
          decoration: BoxDecoration(
            color: const Color(0xFFF8F0E8),
            borderRadius: BorderRadius.circular(8),
            border: Border.all(color: const Color(0xFFD7B9A0)),
          ),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                label,
                style: const TextStyle(fontSize: 12, color: Color(0xFF5E4335)),
              ),
              const SizedBox(height: 2),
              Text(
                '${entry.value}',
                style: const TextStyle(
                  fontSize: 18,
                  fontWeight: FontWeight.w700,
                  color: Color(0xFF8F2F18),
                ),
              ),
            ],
          ),
        );
      }).toList(),
    );
  }
}

enum AuthView { login, register, forgotPassword, resetPassword }
