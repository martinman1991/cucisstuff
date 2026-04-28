<?php
/**
 * API végpont a WPF alkalmazás számára
 * 
 * Ez a fájl fogadja a HTTP kéréseket a C# programból,
 * végrehajtja az SQL műveleteket, és JSON választ küld vissza.
 * 
 * Feltöltési hely: public_html/api.php (vagy a többi PHP fájl mellé)
 * Elérési URL: https://cuci.ady.pepa.hu/api.php
 */

// Hibák megjelenítése (fejlesztéshez – élesben kapcsold ki)
ini_set('display_errors', 0);
error_reporting(0);

// JSON válasz fejléc
header('Content-Type: application/json; charset=utf-8');

// ============================================================
// 1. TOKEN ELLENŐRZÉS
// ============================================================
// IDE ÍRD BE A SAJÁT TITKOS TOKENEDET!
// Használj minimum 20 karaktert, kis-nagy betűket, számokat
$SECRET_TOKEN = 'AzEnTitkosTokenem2024_CucisStuff!XyZ';

// Token kiolvasása a HTTP fejlécből
$clientToken = $_SERVER['HTTP_X_API_TOKEN'] ?? '';

if ($clientToken !== $SECRET_TOKEN) {
    http_response_code(403);
    echo json_encode(['error' => 'Forbidden: Invalid API token']);
    exit;
}

// ============================================================
// 2. ADATBÁZIS KAPCSOLAT
// ============================================================
require_once 'config.php';

try {
    $pdo = new PDO(
        "mysql:host=" . DB_HOST . ";dbname=" . DB_NAME . ";charset=utf8mb4",
        DB_USER,
        DB_PASS,
        [
            PDO::ATTR_ERRMODE            => PDO::ERRMODE_EXCEPTION,
            PDO::ATTR_DEFAULT_FETCH_MODE  => PDO::FETCH_ASSOC,
            PDO::ATTR_EMULATE_PREPARES    => false,
        ]
    );
} catch (PDOException $e) {
    http_response_code(500);
    echo json_encode(['error' => 'Database connection failed: ' . $e->getMessage()]);
    exit;
}

// ============================================================
// 3. BEMENET FELDOLGOZÁSA
// ============================================================
$input = json_decode(file_get_contents('php://input'), true);

if (!$input || !isset($input['query'])) {
    http_response_code(400);
    echo json_encode(['error' => 'Invalid input: missing query']);
    exit;
}

$query      = $input['query'];
$parameters = $input['params'] ?? [];
$type       = $input['type'] ?? 'select';  // select, scalar, insert, update, delete

// ============================================================
// 4. LEKÉRDEZÉS VÉGREHAJTÁSA
// ============================================================
try {
    $stmt = $pdo->prepare($query);
    $stmt->execute($parameters);

    switch ($type) {
        // Több sor visszaadása (SELECT)
        case 'select':
            $rows = $stmt->fetchAll();
            echo json_encode(['data' => $rows]);
            break;

        // Egyetlen érték visszaadása (pl. COUNT, vagy egy mező)
        case 'scalar':
            $value = $stmt->fetchColumn();
            echo json_encode(['data' => $value]);
            break;

        // Beszúrás (INSERT)
        case 'insert':
            $affected = $stmt->rowCount();
            $lastId   = $pdo->lastInsertId();
            echo json_encode([
                'success'  => true,
                'affected' => $affected,
                'lastId'   => $lastId
            ]);
            break;

        // Módosítás (UPDATE) vagy Törlés (DELETE)
        case 'update':
        case 'delete':
            $affected = $stmt->rowCount();
            echo json_encode([
                'success'  => true,
                'affected' => $affected
            ]);
            break;

        default:
            http_response_code(400);
            echo json_encode(['error' => 'Invalid type: ' . $type]);
            break;
    }

} catch (PDOException $e) {
    http_response_code(500);
    echo json_encode(['error' => 'Query failed: ' . $e->getMessage()]);
}