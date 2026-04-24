<?php
session_start();

ini_set('display_errors', 1);
ini_set('display_startup_errors', 1);
error_reporting(E_ALL);

// =============================================
// BEJELENTKEZÉS ELLENŐRZÉS
// =============================================
if (!isset($_SESSION['logged_in']) || $_SESSION['logged_in'] !== true) {
    header("Location: index.php");
    exit();
}

// Kijelentkezés
if (isset($_POST['logout'])) {
    $_SESSION = array();
    session_destroy();
    header("Location: index.php");
    exit();
}

require_once 'config.php';

try {
    $conn = new PDO("mysql:host=" . DB_HOST . ";dbname=" . DB_NAME, DB_USER, DB_PASS);
    $conn->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);

    $userId = (int)$_SESSION['user_id'];

    // Admin ellenőrzés
    $adminCheck = $conn->prepare("SELECT COUNT(*) FROM admins WHERE user_id = ?");
    $adminCheck->execute([$userId]);
    $isAdmin = $adminCheck->fetchColumn() > 0;

    // Olvasatlan üzenetek száma (navbar badge-hoz)
    $unreadStmt = $conn->prepare("SELECT COUNT(*) FROM uzenetek WHERE receiver_id = ? AND is_read = 0");
    $unreadStmt->execute([$userId]);
    $unreadMsgCount = (int)$unreadStmt->fetchColumn();

    // =============================================
    // AJAX: OLVASATLAN ÜZENETEK SZÁMA
    // =============================================
    if (isset($_GET['get_unread_count'])) {
        header('Content-Type: application/json');
        $lastMsgStmt = $conn->prepare("
            SELECT u.username AS sender_name, m.message, m.sent_at
            FROM uzenetek m
            JOIN users u ON m.sender_id = u.id
            WHERE m.receiver_id = ? AND m.is_read = 0
            ORDER BY m.sent_at DESC
            LIMIT 1
        ");
        $lastMsgStmt->execute([$userId]);
        $lastMsg = $lastMsgStmt->fetch(PDO::FETCH_ASSOC);
        echo json_encode([
            'unread_count' => $unreadMsgCount,
            'last_message' => $lastMsg ? [
                'sender'  => $lastMsg['sender_name'],
                'preview' => mb_substr($lastMsg['message'], 0, 50) . (mb_strlen($lastMsg['message']) > 50 ? '…' : ''),
            ] : null
        ]);
        exit;
    }

    // =============================================
    // AJAX: TERMÉK ADATOK LEKÉRÉSE
    // =============================================
    if (isset($_GET['get_item']) && !empty($_GET['get_item'])) {
        header('Content-Type: application/json');
        $itemId = $_GET['get_item'];
        $stmt = $conn->prepare("
            SELECT i.id, i.title, i.description, i.price, i.created_at,
                   u.username AS seller_name, u.id AS seller_id, u.profile_picture,
                   (SELECT COUNT(*) FROM admins WHERE user_id = u.id) AS seller_is_admin
            FROM items i
            JOIN users u ON i.user_id = u.id
            WHERE i.id = ?
        ");
        $stmt->execute([$itemId]);
        $item = $stmt->fetch(PDO::FETCH_ASSOC);
        if (!$item) {
            echo json_encode(['error' => 'Termék nem található']);
            exit;
        }
        $imgStmt = $conn->prepare("SELECT image_path FROM item_images WHERE item_id = ? ORDER BY sort_order");
        $imgStmt->execute([$itemId]);
        $item['images'] = $imgStmt->fetchAll(PDO::FETCH_COLUMN);
        echo json_encode($item);
        exit;
    }

    // =============================================
    // KOSÁR KEZELÉS (session alapú)
    // =============================================

    // Kosár inicializálása
    if (!isset($_SESSION['cart'])) {
        $_SESSION['cart'] = [];
    }

    // AJAX: Kosárba rakás
    if ($_SERVER['REQUEST_METHOD'] === 'POST' && isset($_POST['add_to_cart'])) {
        header('Content-Type: application/json');
        $itemId = trim($_POST['item_id'] ?? '');

        if (empty($itemId)) {
            echo json_encode(['success' => false, 'message' => 'Érvénytelen termék azonosító.']);
            exit;
        }

        // Termék lekérése
        $stmt = $conn->prepare("
            SELECT i.id, i.title, i.price, i.user_id,
                   (SELECT image_path FROM item_images WHERE item_id = i.id AND is_primary = 1 LIMIT 1) AS thumb
            FROM items i WHERE i.id = ?
        ");
        $stmt->execute([$itemId]);
        $item = $stmt->fetch(PDO::FETCH_ASSOC);

        if (!$item) {
            echo json_encode(['success' => false, 'message' => 'A termék nem található.']);
            exit;
        }

        if ((int)$item['user_id'] === $userId) {
            echo json_encode(['success' => false, 'message' => 'Saját termékedet nem teheted a kosárba.']);
            exit;
        }

        if (isset($_SESSION['cart'][$itemId])) {
            echo json_encode(['success' => false, 'message' => 'Ez a termék már a kosaradban van.']);
            exit;
        }

        $_SESSION['cart'][$itemId] = [
            'id'    => $item['id'],
            'title' => $item['title'],
            'price' => (float)$item['price'],
            'thumb' => $item['thumb'],
        ];

        echo json_encode([
            'success'    => true,
            'message'    => 'Termék a kosárba rakva!',
            'cart_count' => count($_SESSION['cart']),
        ]);
        exit;
    }

    // AJAX: Eltávolítás kosárból
    if ($_SERVER['REQUEST_METHOD'] === 'POST' && isset($_POST['remove_from_cart'])) {
        header('Content-Type: application/json');
        $itemId = trim($_POST['item_id'] ?? '');
        if (isset($_SESSION['cart'][$itemId])) {
            unset($_SESSION['cart'][$itemId]);
            echo json_encode(['success' => true, 'cart_count' => count($_SESSION['cart'])]);
        } else {
            echo json_encode(['success' => false, 'message' => 'A termék nincs a kosárban.']);
        }
        exit;
    }

    // AJAX: Kosár törlése
    if ($_SERVER['REQUEST_METHOD'] === 'POST' && isset($_POST['clear_cart'])) {
        header('Content-Type: application/json');
        $_SESSION['cart'] = [];
        echo json_encode(['success' => true]);
        exit;
    }

    // =============================================
    // RENDELÉS LEADÁSA
    // =============================================
    $orderSuccess = false;
    $orderError   = '';

    if ($_SERVER['REQUEST_METHOD'] === 'POST' && isset($_POST['place_order'])) {
        $nev     = trim($_POST['nev']     ?? '');
        $email   = trim($_POST['email']   ?? '');
        $cim     = trim($_POST['cim']     ?? '');
        $varos   = trim($_POST['varos']   ?? '');
        $irsz    = trim($_POST['irsz']    ?? '');
        $megjegy = trim($_POST['megjegyzes'] ?? '');

        if (empty($nev) || empty($email) || empty($cim) || empty($varos) || empty($irsz)) {
            $orderError = 'Kérjük, töltsd ki az összes kötelező mezőt!';
        } elseif (!filter_var($email, FILTER_VALIDATE_EMAIL)) {
            $orderError = 'Érvénytelen email cím.';
        } elseif (empty($_SESSION['cart'])) {
            $orderError = 'A kosarad üres.';
        } else {
            // Ellenőrzés: van-e már eladott/nem létező termék?
            $cartIds = array_keys($_SESSION['cart']);
            $placeholders = implode(',', array_fill(0, count($cartIds), '?'));
            $checkStmt = $conn->prepare("SELECT id FROM items WHERE id IN ($placeholders)");
            $checkStmt->execute($cartIds);
            $existingIds = $checkStmt->fetchAll(PDO::FETCH_COLUMN);

            $missing = array_diff($cartIds, $existingIds);
            if (!empty($missing)) {
                // Eltávolítjuk a már nem létező termékeket
                foreach ($missing as $mid) {
                    unset($_SESSION['cart'][$mid]);
                }
                $orderError = 'Egy vagy több termék időközben eltűnt. A kosár frissítve. Kérjük, ellenőrizd és próbáld újra.';
            } else {
                // Rendelés sikeres — üzenetet küldünk az eladóknak
                $conn->beginTransaction();
                try {
                    // Eladók csoportosítása
                    $sellerItems = [];
                    foreach ($_SESSION['cart'] as $cItemId => $cItem) {
                        $sellerStmt = $conn->prepare("SELECT user_id FROM items WHERE id = ?");
                        $sellerStmt->execute([$cItemId]);
                        $sellerId = (int)$sellerStmt->fetchColumn();
                        if (!isset($sellerItems[$sellerId])) {
                            $sellerItems[$sellerId] = [];
                        }
                        $sellerItems[$sellerId][] = $cItem;
                    }

                    // Üzenet küldése minden eladónak
                    foreach ($sellerItems as $sellerId => $sItems) {
                        if ($sellerId === $userId) continue; // Saját termékek (elvileg nem kerülhet kosárba)

                        $itemList = '';
                        $total = 0;
                        foreach ($sItems as $si) {
                            $itemList .= '• ' . $si['title'] . ' — ' . number_format($si['price'], 0, ',', ' ') . ' Ft' . "\n";
                            $total += $si['price'];
                        }

                        $msgText =
                            "🛒 Új vásárlási megkeresés!\n\n" .
                            "Terméke(i):\n" . $itemList . "\n" .
                            "Összesen: " . number_format($total, 0, ',', ' ') . " Ft\n\n" .
                            "Szállítási adatok:\n" .
                            "Név: $nev\n" .
                            "Email: $email\n" .
                            "Cím: $irsz $varos, $cim\n" .
                            ($megjegy ? "Megjegyzés: $megjegy\n" : "") .
                            "\nKérjük, vedd fel a kapcsolatot a vevővel az egyeztetéshez.";

                        // Üzenet azonosító generálása (25 karakter, mint az uzenetek táblában)
                        $msgId = substr(str_shuffle('abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789'), 0, 25);

                        $msgStmt = $conn->prepare("
                            INSERT INTO uzenetek (id, sender_id, receiver_id, message, is_read)
                            VALUES (?, ?, ?, ?, 0)
                        ");
                        $msgStmt->execute([$msgId, $userId, $sellerId, $msgText]);
                    }

                    $conn->commit();
                    $_SESSION['cart'] = [];
                    $orderSuccess = true;

                } catch (Exception $e) {
                    $conn->rollBack();
                    $orderError = 'Hiba történt a rendelés feldolgozása során. Kérjük, próbáld újra.';
                }
            }
        }
    }

    // =============================================
    // TERMÉKEK LISTÁJA (főoldal grid)
    // =============================================
    $page    = max(1, (int)($_GET['page'] ?? 1));
    $perPage = 20;
    $offset  = ($page - 1) * $perPage;

    $search   = trim($_GET['q'] ?? '');
    $minPrice = isset($_GET['min_price']) && is_numeric($_GET['min_price']) ? (float)$_GET['min_price'] : null;
    $maxPrice = isset($_GET['max_price']) && is_numeric($_GET['max_price']) ? (float)$_GET['max_price'] : null;
    $sort     = $_GET['sort'] ?? 'newest';

    $where  = [];
    $params = [];

    if (!empty($search)) {
        $where[]  = "(i.title LIKE :q OR i.description LIKE :q)";
        $params[':q'] = '%' . $search . '%';
    }
    if ($minPrice !== null) {
        $where[]          = "i.price >= :min_price";
        $params[':min_price'] = $minPrice;
    }
    if ($maxPrice !== null) {
        $where[]          = "i.price <= :max_price";
        $params[':max_price'] = $maxPrice;
    }

    $whereSQL = $where ? 'WHERE ' . implode(' AND ', $where) : '';

    $orderSQL = match ($sort) {
        'price_asc'  => 'ORDER BY i.price ASC',
        'price_desc' => 'ORDER BY i.price DESC',
        'oldest'     => 'ORDER BY i.created_at ASC',
        default      => 'ORDER BY i.created_at DESC',
    };

    // Összes találat száma lapozáshoz
    $countStmt = $conn->prepare("SELECT COUNT(*) FROM items i $whereSQL");
    $countStmt->execute($params);
    $totalItems = (int)$countStmt->fetchColumn();
    $totalPages = (int)ceil($totalItems / $perPage);

    // Termékek lekérése
    $itemStmt = $conn->prepare("
        SELECT i.id, i.title, i.price, i.created_at,
               u.username AS seller_name, u.id AS seller_id,
               (SELECT image_path FROM item_images WHERE item_id = i.id AND is_primary = 1 LIMIT 1) AS primary_image,
               (SELECT COUNT(*) FROM item_images WHERE item_id = i.id) AS image_count
        FROM items i
        JOIN users u ON i.user_id = u.id
        $whereSQL
        $orderSQL
        LIMIT :limit OFFSET :offset
    ");
    foreach ($params as $k => $v) $itemStmt->bindValue($k, $v);
    $itemStmt->bindValue(':limit',  $perPage, PDO::PARAM_INT);
    $itemStmt->bindValue(':offset', $offset,  PDO::PARAM_INT);
    $itemStmt->execute();
    $items = $itemStmt->fetchAll(PDO::FETCH_ASSOC);

    // Saját felhasználó adatai (navbar)
    $meStmt = $conn->prepare("SELECT username, profile_picture FROM users WHERE id = ?");
    $meStmt->execute([$userId]);
    $me = $meStmt->fetch(PDO::FETCH_ASSOC);

    $cartCount = count($_SESSION['cart']);

} catch (PDOException $e) {
    die("Adatbázis hiba: " . htmlspecialchars($e->getMessage()));
}
?>
<!DOCTYPE html>
<html lang="hu">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>Vásárlás – Cuci's Stuff</title>
    <link rel="preconnect" href="https://fonts.googleapis.com">
    <link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
    <link href="https://fonts.googleapis.com/css2?family=Nunito:wght@300;400;600;700;800&display=swap" rel="stylesheet">
    <link rel="icon" type="image/png" href="logo.png">
    <link rel="stylesheet" href="theme-dark.css" id="themeStylesheet">

    <!-- FOUC megelőzés -->
    <script>
        (function () {
            var t = localStorage.getItem('theme') || 'dark';
            document.documentElement.setAttribute('data-theme', t);
            if (t === 'light') {
                document.getElementById('themeStylesheet').href = 'theme-light.css';
            }
        })();
    </script>

    <style>
        /* ══════════════════════════════════════════
           CSS ALAP + VÁLTOZÓK
           ══════════════════════════════════════════ */
        *, *::before, *::after { box-sizing: border-box; margin: 0; padding: 0; }

        body {
            font-family: 'Nunito', sans-serif;
            background: var(--body-bg, #000);
            color: var(--text-primary, #f5f0e8);
            min-height: 100vh;
            padding-top: 72px;
        }

        body::before, body::after {
            content: ''; position: fixed; inset: 0; pointer-events: none; z-index: 0;
        }

        /* ══════════════════════════════════════════
           TOP NAVIGÁCIÓ
           ══════════════════════════════════════════ */
        .topbar {
            position: fixed; top: 0; left: 0; right: 0; z-index: 1000;
            height: 64px;
            background: rgba(0,0,0,0.85);
            backdrop-filter: blur(18px);
            border-bottom: 1px solid var(--glass-border, rgba(255,140,0,.18));
            display: flex; align-items: center;
            padding: 0 1.5rem; gap: 1rem;
        }

        .topbar-logo {
            font-size: 1.35rem; font-weight: 800; letter-spacing: 2px;
            color: var(--orange-bright, #ff9a1f);
            text-decoration: none; white-space: nowrap;
        }
        .topbar-logo span { color: var(--text-primary, #f5f0e8); }

        .topbar-search {
            flex: 1; max-width: 480px;
            display: flex; gap: .5rem;
        }
        .topbar-search input {
            flex: 1; padding: .55rem 1rem;
            background: var(--input-bg, rgba(20,16,10,.92));
            border: 1px solid var(--glass-border, rgba(255,140,0,.18));
            border-radius: 12px; color: var(--text-primary, #f5f0e8);
            font-family: inherit; font-size: .9rem;
            outline: none; transition: border-color .2s, box-shadow .2s;
        }
        .topbar-search input:focus {
            border-color: var(--orange-bright, #ff9a1f);
            box-shadow: 0 0 0 3px rgba(255,140,0,.15);
        }
        .topbar-search button {
            padding: .55rem 1.1rem;
            background: linear-gradient(135deg, var(--orange-bright, #ff9a1f), var(--orange-mid, #e07800));
            color: #000; border: none; border-radius: 12px;
            cursor: pointer; font-weight: 700; font-size: .9rem;
            font-family: inherit; transition: opacity .2s;
        }
        .topbar-search button:hover { opacity: .85; }

        .topbar-actions { margin-left: auto; display: flex; align-items: center; gap: .75rem; }

        .topbar-btn {
            padding: .5rem 1rem; border-radius: 12px;
            border: 1px solid var(--glass-border, rgba(255,140,0,.18));
            background: rgba(255,140,0,.08); color: var(--orange-bright, #ff9a1f);
            text-decoration: none; font-size: .88rem; font-weight: 600;
            cursor: pointer; transition: all .2s; font-family: inherit;
            display: flex; align-items: center; gap: .4rem; white-space: nowrap;
        }
        .topbar-btn:hover {
            background: rgba(255,140,0,.2);
            border-color: var(--orange-bright, #ff9a1f);
        }
        .topbar-btn.active {
            background: linear-gradient(135deg, var(--orange-bright, #ff9a1f), var(--orange-mid, #e07800));
            color: #000; border-color: transparent;
        }

        .cart-badge {
            display: inline-flex; align-items: center; justify-content: center;
            background: #e03030; color: #fff;
            width: 20px; height: 20px; border-radius: 50%;
            font-size: .72rem; font-weight: 800; line-height: 1;
        }

        .msg-badge {
            display: inline-flex; align-items: center; justify-content: center;
            background: #007bff; color: #fff;
            width: 20px; height: 20px; border-radius: 50%;
            font-size: .72rem; font-weight: 800; line-height: 1;
        }
        .msg-badge.hidden { display: none; }

        /* ══════════════════════════════════════════
           ACCOUNT DROPDOWN
           ══════════════════════════════════════════ */
        .account-menu-wrap { position: relative; }
        .account-menu-btn {
            padding: .5rem 1rem; border-radius: 12px;
            border: 1px solid var(--glass-border, rgba(255,140,0,.18));
            background: rgba(0,0,0,.4); color: var(--orange-bright, #ff9a1f);
            cursor: pointer; font-family: inherit; font-size: .88rem;
            font-weight: 600; display: flex; align-items: center; gap: .5rem;
            transition: all .2s;
        }
        .account-menu-btn:hover { background: rgba(255,140,0,.15); }
        .account-avatar-sm {
            width: 28px; height: 28px; border-radius: 50%;
            object-fit: cover; border: 1px solid var(--orange-bright, #ff9a1f);
        }
        .account-avatar-placeholder-sm {
            width: 28px; height: 28px; border-radius: 50%;
            background: linear-gradient(135deg, var(--orange-bright, #ff9a1f), var(--orange-mid, #e07800));
            display: flex; align-items: center; justify-content: center;
            font-weight: 800; font-size: .85rem; color: #000;
        }
        .account-dropdown-panel {
            display: none; position: absolute; right: 0; top: calc(100% + .5rem);
            min-width: 200px; background: rgba(8,8,8,.98);
            border: 1px solid rgba(255,140,0,.25); border-radius: 16px;
            padding: .75rem; box-shadow: 0 16px 40px rgba(0,0,0,.6);
            backdrop-filter: blur(16px); z-index: 9999;
        }
        .account-dropdown-panel.show { display: block; }
        .dropdown-username { font-weight: 700; color: var(--orange-bright, #ff9a1f); padding: .4rem .75rem .6rem; font-size: .9rem; }
        .dropdown-divider { height: 1px; background: rgba(255,140,0,.2); margin: .4rem 0; }
        .dropdown-item {
            display: block; padding: .55rem .75rem; border-radius: 10px;
            color: var(--text-primary, #f5f0e8); text-decoration: none;
            font-size: .88rem; transition: all .2s; border: 1px solid transparent;
        }
        .dropdown-item:hover { background: rgba(255,140,0,.15); color: var(--orange-bright, #ff9a1f); }
        .logout-form-btn {
            width: 100%; text-align: left; background: none; border: none;
            padding: .55rem .75rem; border-radius: 10px;
            color: var(--text-primary, #f5f0e8); font-family: inherit;
            font-size: .88rem; cursor: pointer; transition: all .2s;
        }
        .logout-form-btn:hover { background: rgba(255,140,0,.15); color: #ff4444; }

        /* ══════════════════════════════════════════
           FŐ TARTALOM ELRENDEZÉS
           ══════════════════════════════════════════ */
        .page-wrap {
            max-width: 1400px; margin: 0 auto;
            padding: 1.5rem 1.5rem 3rem;
            display: grid;
            grid-template-columns: 1fr 360px;
            gap: 1.5rem;
            position: relative; z-index: 1;
        }

        @media (max-width: 1024px) {
            .page-wrap { grid-template-columns: 1fr; }
            .cart-sidebar { order: -1; }
        }

        /* ══════════════════════════════════════════
           SZŰRŐ SOR
           ══════════════════════════════════════════ */
        .filter-bar {
            display: flex; align-items: center; gap: .75rem;
            flex-wrap: wrap; margin-bottom: 1.25rem;
        }
        .filter-bar label { font-size: .85rem; color: var(--text-muted, #8a7a65); }
        .filter-select, .filter-input {
            padding: .45rem .85rem; border-radius: 10px;
            background: var(--input-bg, rgba(20,16,10,.92));
            border: 1px solid var(--glass-border, rgba(255,140,0,.18));
            color: var(--text-primary, #f5f0e8);
            font-family: inherit; font-size: .88rem; outline: none;
        }
        .filter-select:focus, .filter-input:focus {
            border-color: var(--orange-bright, #ff9a1f);
        }
        .filter-input { width: 110px; }
        .filter-btn {
            padding: .45rem 1rem; border-radius: 10px;
            background: rgba(255,140,0,.12);
            border: 1px solid var(--glass-border, rgba(255,140,0,.18));
            color: var(--orange-bright, #ff9a1f);
            font-family: inherit; font-size: .88rem;
            cursor: pointer; transition: all .2s;
        }
        .filter-btn:hover { background: rgba(255,140,0,.25); }
        .results-count { margin-left: auto; font-size: .85rem; color: var(--text-muted, #8a7a65); }

        /* ══════════════════════════════════════════
           TERMÉK GRID
           ══════════════════════════════════════════ */
        .items-grid {
            display: grid;
            grid-template-columns: repeat(auto-fill, minmax(220px, 1fr));
            gap: 1rem;
        }

        .item-card {
            background: rgba(0,0,0,.45);
            border: 1px solid rgba(255,140,0,.18);
            border-radius: 18px; overflow: hidden;
            cursor: pointer; transition: all .25s;
            display: flex; flex-direction: column;
        }
        .item-card:hover {
            border-color: var(--orange-bright, #ff9a1f);
            transform: translateY(-3px);
            box-shadow: 0 12px 32px rgba(255,140,0,.18);
        }
        .item-card.in-cart {
            border-color: rgba(0,200,100,.5);
            box-shadow: 0 0 20px rgba(0,200,100,.12);
        }

        .item-img-wrap {
            width: 100%; aspect-ratio: 4/3; overflow: hidden;
            background: rgba(255,140,0,.07); position: relative;
        }
        .item-img-wrap img {
            width: 100%; height: 100%; object-fit: cover;
            transition: transform .3s;
        }
        .item-card:hover .item-img-wrap img { transform: scale(1.04); }
        .item-img-placeholder {
            width: 100%; height: 100%;
            display: flex; align-items: center; justify-content: center;
            font-size: 2.5rem; color: rgba(255,140,0,.3);
        }
        .img-count-badge {
            position: absolute; bottom: 6px; right: 8px;
            background: rgba(0,0,0,.7); border: 1px solid rgba(255,140,0,.3);
            color: var(--orange-bright, #ff9a1f);
            font-size: .72rem; font-weight: 700;
            padding: 2px 7px; border-radius: 20px;
        }
        .in-cart-badge {
            position: absolute; top: 8px; left: 8px;
            background: rgba(0,200,100,.85); color: #000;
            font-size: .72rem; font-weight: 800;
            padding: 3px 9px; border-radius: 20px;
        }

        .item-body { padding: .85rem .9rem 1rem; flex: 1; display: flex; flex-direction: column; gap: .3rem; }
        .item-title { font-size: .95rem; font-weight: 700; color: var(--orange-bright, #ff9a1f); line-height: 1.3; }
        .item-price { font-size: 1.05rem; font-weight: 800; margin-top: auto; }
        .item-seller { font-size: .78rem; color: var(--text-muted, #8a7a65); }

        .item-add-btn {
            display: block; width: calc(100% - 1.8rem);
            margin: 0 .9rem .9rem;
            padding: .55rem; border-radius: 12px;
            background: linear-gradient(135deg, var(--orange-bright, #ff9a1f), var(--orange-mid, #e07800));
            color: #000; border: none;
            font-family: inherit; font-size: .85rem; font-weight: 700;
            cursor: pointer; transition: opacity .2s, transform .15s;
        }
        .item-add-btn:hover { opacity: .88; }
        .item-add-btn:active { transform: scale(.97); }
        .item-add-btn.already-in { background: linear-gradient(135deg, #2ecc71, #27ae60); cursor: default; }
        .item-add-btn.own-item { background: rgba(255,140,0,.15); color: var(--text-muted, #8a7a65); cursor: not-allowed; }

        /* ══════════════════════════════════════════
           ÜRES ÁLLAPOT
           ══════════════════════════════════════════ */
        .empty-state {
            grid-column: 1 / -1; text-align: center;
            padding: 4rem 2rem; color: var(--text-muted, #8a7a65);
        }
        .empty-state .empty-icon { font-size: 4rem; margin-bottom: 1rem; display: block; }
        .empty-state h3 { font-size: 1.2rem; margin-bottom: .5rem; }

        /* ══════════════════════════════════════════
           LAPOZÓ
           ══════════════════════════════════════════ */
        .pagination {
            display: flex; align-items: center; justify-content: center;
            gap: .5rem; margin-top: 1.5rem; flex-wrap: wrap;
        }
        .page-btn {
            padding: .45rem .85rem; border-radius: 10px;
            background: rgba(255,140,0,.08);
            border: 1px solid var(--glass-border, rgba(255,140,0,.18));
            color: var(--text-primary, #f5f0e8);
            text-decoration: none; font-size: .88rem;
            transition: all .2s;
        }
        .page-btn:hover, .page-btn.current {
            background: rgba(255,140,0,.25);
            border-color: var(--orange-bright, #ff9a1f);
            color: var(--orange-bright, #ff9a1f);
        }
        .page-btn.current { font-weight: 800; }

        /* ══════════════════════════════════════════
           KOSÁR OLDALSÁV
           ══════════════════════════════════════════ */
        .cart-sidebar {
            position: sticky; top: 80px; height: fit-content;
            background: rgba(0,0,0,.55);
            border: 1px solid rgba(255,140,0,.2);
            border-radius: 22px; padding: 1.25rem;
            backdrop-filter: blur(12px);
        }

        .cart-title {
            font-size: 1.15rem; font-weight: 800;
            color: var(--orange-bright, #ff9a1f);
            display: flex; align-items: center; gap: .6rem;
            margin-bottom: 1rem;
        }
        .cart-count-pill {
            background: var(--orange-bright, #ff9a1f); color: #000;
            font-size: .75rem; font-weight: 800;
            padding: 2px 8px; border-radius: 20px;
        }

        .cart-items-list { display: flex; flex-direction: column; gap: .6rem; max-height: 380px; overflow-y: auto; }
        .cart-items-list::-webkit-scrollbar { width: 4px; }
        .cart-items-list::-webkit-scrollbar-track { background: transparent; }
        .cart-items-list::-webkit-scrollbar-thumb { background: rgba(255,140,0,.3); border-radius: 2px; }

        .cart-item {
            display: flex; align-items: center; gap: .7rem;
            padding: .6rem .7rem; border-radius: 12px;
            background: rgba(255,140,0,.05);
            border: 1px solid rgba(255,140,0,.12);
        }
        .cart-item-thumb {
            width: 46px; height: 46px; border-radius: 8px;
            object-fit: cover; border: 1px solid rgba(255,140,0,.2);
            flex-shrink: 0;
        }
        .cart-item-thumb-placeholder {
            width: 46px; height: 46px; border-radius: 8px;
            background: rgba(255,140,0,.1);
            display: flex; align-items: center; justify-content: center;
            font-size: 1.3rem; flex-shrink: 0;
        }
        .cart-item-info { flex: 1; min-width: 0; }
        .cart-item-name { font-size: .83rem; font-weight: 700; color: var(--text-primary, #f5f0e8); white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
        .cart-item-price { font-size: .8rem; color: var(--orange-bright, #ff9a1f); font-weight: 600; }
        .cart-item-remove {
            background: none; border: none; color: rgba(255,80,80,.7);
            font-size: 1.1rem; cursor: pointer; padding: .2rem;
            transition: color .2s; flex-shrink: 0;
        }
        .cart-item-remove:hover { color: #ff4444; }

        .cart-empty-msg {
            text-align: center; padding: 2rem 0;
            color: var(--text-muted, #8a7a65); font-size: .9rem;
        }
        .cart-empty-msg span { font-size: 2.5rem; display: block; margin-bottom: .5rem; }

        .cart-total-row {
            display: flex; justify-content: space-between; align-items: center;
            padding: .9rem 0 .6rem;
            border-top: 1px solid rgba(255,140,0,.15);
            margin-top: .75rem;
        }
        .cart-total-label { font-size: .9rem; color: var(--text-muted, #8a7a65); }
        .cart-total-amount { font-size: 1.2rem; font-weight: 800; color: var(--orange-bright, #ff9a1f); }

        .cart-checkout-btn {
            display: block; width: 100%; padding: .85rem;
            background: linear-gradient(135deg, var(--orange-bright, #ff9a1f), var(--orange-mid, #e07800));
            color: #000; border: none; border-radius: 14px;
            font-family: inherit; font-size: 1rem; font-weight: 800;
            cursor: pointer; transition: opacity .2s, transform .15s;
            margin-top: .75rem;
        }
        .cart-checkout-btn:hover { opacity: .88; }
        .cart-checkout-btn:active { transform: scale(.98); }
        .cart-checkout-btn:disabled { opacity: .4; cursor: not-allowed; }

        .cart-clear-btn {
            display: block; width: 100%; padding: .55rem;
            background: rgba(255,80,80,.08);
            border: 1px solid rgba(255,80,80,.2);
            color: rgba(255,100,100,.8); border-radius: 10px;
            font-family: inherit; font-size: .83rem; font-weight: 600;
            cursor: pointer; transition: all .2s; margin-top: .5rem;
        }
        .cart-clear-btn:hover { background: rgba(255,80,80,.18); color: #ff4444; }

        /* ══════════════════════════════════════════
           PÉNZTÁR MODAL
           ══════════════════════════════════════════ */
        .checkout-overlay {
            display: none; position: fixed; inset: 0;
            background: rgba(0,0,0,.92); z-index: 3000;
            align-items: center; justify-content: center;
            padding: 1rem;
        }
        .checkout-overlay.active { display: flex; }

        .checkout-modal {
            background: rgba(8,8,8,.98);
            border: 1px solid rgba(255,140,0,.25);
            border-radius: 24px; padding: 2rem;
            width: 100%; max-width: 500px;
            max-height: 90vh; overflow-y: auto;
        }
        .checkout-modal h2 {
            font-size: 1.4rem; color: var(--orange-bright, #ff9a1f);
            margin-bottom: 1.25rem;
            display: flex; align-items: center; gap: .6rem;
        }

        .co-form-group { margin-bottom: 1rem; }
        .co-form-group label { display: block; font-size: .85rem; font-weight: 600; margin-bottom: .35rem; color: var(--text-muted, #8a7a65); }
        .co-form-group label span { color: #ff4444; }
        .co-input, .co-textarea {
            width: 100%; padding: .7rem 1rem;
            background: rgba(20,16,10,.92);
            border: 1px solid rgba(255,140,0,.18);
            border-radius: 12px; color: var(--text-primary, #f5f0e8);
            font-family: inherit; font-size: .9rem; outline: none;
            transition: border-color .2s, box-shadow .2s;
        }
        .co-input:focus, .co-textarea:focus {
            border-color: var(--orange-bright, #ff9a1f);
            box-shadow: 0 0 0 3px rgba(255,140,0,.15);
        }
        .co-textarea { resize: vertical; min-height: 80px; }

        .co-row { display: grid; grid-template-columns: 1fr 1fr; gap: 1rem; }

        .co-order-summary {
            background: rgba(255,140,0,.06);
            border: 1px solid rgba(255,140,0,.15);
            border-radius: 14px; padding: 1rem; margin-bottom: 1.25rem;
        }
        .co-order-summary h3 { font-size: .9rem; color: var(--text-muted, #8a7a65); margin-bottom: .6rem; }
        .co-summary-line {
            display: flex; justify-content: space-between;
            font-size: .88rem; padding: .2rem 0;
            border-bottom: 1px solid rgba(255,140,0,.08);
        }
        .co-summary-line:last-child { border-bottom: none; font-weight: 800; color: var(--orange-bright, #ff9a1f); font-size: 1rem; }
        .co-summary-name { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; max-width: 260px; }

        .co-actions { display: flex; gap: .75rem; margin-top: 1.25rem; }
        .co-submit-btn {
            flex: 1; padding: .85rem;
            background: linear-gradient(135deg, var(--orange-bright, #ff9a1f), var(--orange-mid, #e07800));
            color: #000; border: none; border-radius: 14px;
            font-family: inherit; font-size: 1rem; font-weight: 800;
            cursor: pointer; transition: opacity .2s;
        }
        .co-submit-btn:hover { opacity: .88; }
        .co-cancel-btn {
            padding: .85rem 1.5rem; border-radius: 14px;
            background: rgba(255,140,0,.08);
            border: 1px solid rgba(255,140,0,.2);
            color: var(--orange-bright, #ff9a1f);
            font-family: inherit; font-size: 1rem; font-weight: 600;
            cursor: pointer; transition: all .2s;
        }
        .co-cancel-btn:hover { background: rgba(255,140,0,.18); }

        .co-note {
            font-size: .78rem; color: var(--text-muted, #8a7a65);
            margin-top: .75rem; line-height: 1.5;
        }

        /* ══════════════════════════════════════════
           TERMÉK RÉSZLET MODAL
           ══════════════════════════════════════════ */
        .product-overlay {
            display: none; position: fixed; inset: 0;
            background: rgba(0,0,0,.95); z-index: 2000;
            align-items: center; justify-content: center; padding: 1rem;
        }
        .product-overlay.active { display: flex; }

        .product-modal {
            background: rgba(8,8,8,.98);
            border: 1px solid rgba(255,140,0,.2);
            border-radius: 24px; padding: 2rem;
            width: 100%; max-width: 620px;
            max-height: 90vh; overflow-y: auto;
            position: relative;
        }
        .pm-close {
            position: absolute; top: 1rem; right: 1rem;
            background: rgba(255,140,0,.1); border: 1px solid rgba(255,140,0,.2);
            color: var(--orange-bright, #ff9a1f);
            width: 36px; height: 36px; border-radius: 50%;
            display: flex; align-items: center; justify-content: center;
            font-size: 1.1rem; cursor: pointer; transition: all .2s;
        }
        .pm-close:hover { background: var(--orange-bright, #ff9a1f); color: #000; }

        .pm-gallery { position: relative; margin-bottom: 1.25rem; }
        .pm-main-img {
            width: 100%; aspect-ratio: 16/9; object-fit: contain;
            border-radius: 14px; background: rgba(255,140,0,.06);
            border: 1px solid rgba(255,140,0,.12); cursor: zoom-in;
        }
        .pm-no-img {
            width: 100%; aspect-ratio: 16/9;
            border-radius: 14px; background: rgba(255,140,0,.06);
            border: 1px solid rgba(255,140,0,.12);
            display: flex; align-items: center; justify-content: center;
            font-size: 4rem; color: rgba(255,140,0,.3);
        }
        .pm-thumbs { display: flex; gap: .5rem; margin-top: .5rem; flex-wrap: wrap; }
        .pm-thumb {
            width: 56px; height: 56px; border-radius: 8px; overflow: hidden;
            border: 2px solid transparent; cursor: pointer; transition: border-color .2s;
        }
        .pm-thumb.active, .pm-thumb:hover { border-color: var(--orange-bright, #ff9a1f); }
        .pm-thumb img { width: 100%; height: 100%; object-fit: cover; }

        .pm-nav {
            position: absolute; top: 50%; transform: translateY(-50%);
            background: rgba(0,0,0,.7); border: 1px solid rgba(255,140,0,.3);
            color: var(--orange-bright, #ff9a1f);
            width: 36px; height: 36px; border-radius: 50%;
            display: flex; align-items: center; justify-content: center;
            cursor: pointer; font-size: 1rem; transition: all .2s;
        }
        .pm-nav:hover { background: rgba(255,140,0,.3); }
        .pm-nav.prev { left: .5rem; }
        .pm-nav.next { right: .5rem; }
        .pm-nav.hidden { display: none; }

        .pm-title { font-size: 1.4rem; font-weight: 800; color: var(--text-primary, #f5f0e8); margin-bottom: .4rem; }
        .pm-price { font-size: 1.6rem; font-weight: 800; color: var(--orange-bright, #ff9a1f); margin-bottom: .5rem; }
        .pm-meta { font-size: .82rem; color: var(--text-muted, #8a7a65); margin-bottom: .8rem; }
        .pm-desc { font-size: .9rem; line-height: 1.65; margin-bottom: 1.25rem; }

        .pm-add-btn {
            width: 100%; padding: .9rem;
            background: linear-gradient(135deg, var(--orange-bright, #ff9a1f), var(--orange-mid, #e07800));
            color: #000; border: none; border-radius: 14px;
            font-family: inherit; font-size: 1rem; font-weight: 800;
            cursor: pointer; transition: opacity .2s;
        }
        .pm-add-btn:hover { opacity: .88; }
        .pm-add-btn.already-in { background: linear-gradient(135deg, #2ecc71, #27ae60); cursor: default; }
        .pm-add-btn.own-item { background: rgba(255,140,0,.15); color: var(--text-muted, #8a7a65); cursor: not-allowed; }

        /* ══════════════════════════════════════════
           LIGHTBOX
           ══════════════════════════════════════════ */
        .lightbox-overlay {
            display: none; position: fixed; inset: 0;
            background: rgba(0,0,0,.97); z-index: 5000;
            align-items: center; justify-content: center;
            cursor: zoom-out;
        }
        .lightbox-overlay.active { display: flex; }
        .lightbox-overlay img { max-width: 95vw; max-height: 95vh; object-fit: contain; border-radius: 8px; }
        .lightbox-close {
            position: fixed; top: 1rem; right: 1rem;
            background: rgba(255,140,0,.15); border: 1px solid rgba(255,140,0,.3);
            color: var(--orange-bright, #ff9a1f);
            width: 40px; height: 40px; border-radius: 50%;
            display: flex; align-items: center; justify-content: center;
            cursor: pointer; font-size: 1.2rem; transition: all .2s;
        }
        .lightbox-close:hover { background: var(--orange-bright, #ff9a1f); color: #000; }

        /* ══════════════════════════════════════════
           SIKERES RENDELÉS
           ══════════════════════════════════════════ */
        .success-banner {
            background: rgba(0,200,100,.1);
            border: 1px solid rgba(0,200,100,.35);
            border-radius: 16px; padding: 1.5rem;
            margin-bottom: 1.5rem; text-align: center;
            grid-column: 1 / -1;
        }
        .success-banner .success-icon { font-size: 3rem; display: block; margin-bottom: .75rem; }
        .success-banner h2 { color: #2ecc71; margin-bottom: .5rem; font-size: 1.3rem; }
        .success-banner p { color: var(--text-muted, #8a7a65); font-size: .9rem; line-height: 1.6; }

        /* ══════════════════════════════════════════
           TOAST ÉRTESÍTÉS
           ══════════════════════════════════════════ */
        #cartToast {
            position: fixed; bottom: 2rem; right: 2rem; z-index: 9999;
            padding: .75rem 1.25rem;
            background: rgba(10,10,10,.95);
            border: 1px solid rgba(255,140,0,.3);
            border-radius: 40px;
            box-shadow: 0 8px 24px rgba(0,0,0,.5);
            font-size: .9rem; font-weight: 600;
            opacity: 0; transform: translateY(12px);
            transition: all .3s; pointer-events: none;
            max-width: 320px;
        }
        #cartToast.show { opacity: 1; transform: translateY(0); }
        #cartToast.success { border-color: rgba(0,200,100,.5); color: #2ecc71; }
        #cartToast.error { border-color: rgba(255,80,80,.5); color: #ff6060; }
        #cartToast.info { border-color: rgba(255,140,0,.4); color: var(--orange-bright, #ff9a1f); }

        /* ══════════════════════════════════════════
           HIBA SÁTOR
           ══════════════════════════════════════════ */
        .order-error {
            background: rgba(255,60,60,.1);
            border: 1px solid rgba(255,60,60,.35);
            border-radius: 12px; padding: 1rem 1.25rem;
            color: #ff6060; font-size: .9rem;
            margin-bottom: 1rem;
            grid-column: 1 / -1;
        }

        /* Scrollbar */
        ::-webkit-scrollbar { width: 6px; }
        ::-webkit-scrollbar-track { background: transparent; }
        ::-webkit-scrollbar-thumb { background: rgba(255,120,0,.3); border-radius: 3px; }
        ::-webkit-scrollbar-thumb:hover { background: rgba(255,140,0,.5); }
    </style>
</head>
<body>

<!-- ══════════════════════════════════════════
     TOP NAVIGÁCIÓ
     ══════════════════════════════════════════ -->
<nav class="topbar">
    <a href="main.php" class="topbar-logo">Cuci's <span>Stuff</span></a>

    <form class="topbar-search" method="GET" action="vasarlas.php">
        <input type="text" name="q" placeholder="Keresés a termékek között…"
               value="<?php echo htmlspecialchars($search); ?>">
        <button type="submit">🔍</button>
    </form>

    <div class="topbar-actions">
        <a href="main.php" class="topbar-btn">🏠 Főoldal</a>
        <a href="uzenetek.php" class="topbar-btn">
            💬 Üzenetek
            <span class="msg-badge <?php echo $unreadMsgCount > 0 ? '' : 'hidden'; ?>" id="msgBadge">
                <?php echo $unreadMsgCount > 9 ? '9+' : $unreadMsgCount; ?>
            </span>
        </a>

        <!-- Fiók dropdown -->
        <div class="account-menu-wrap">
            <button class="account-menu-btn" id="accountMenuBtn" type="button">
                <?php if (!empty($me['profile_picture']) && file_exists($me['profile_picture'])): ?>
                    <img src="<?php echo htmlspecialchars($me['profile_picture']); ?>"
                         class="account-avatar-sm" alt="avatar">
                <?php else: ?>
                    <div class="account-avatar-placeholder-sm">
                        <?php echo mb_strtoupper(mb_substr($me['username'] ?? 'U', 0, 1)); ?>
                    </div>
                <?php endif; ?>
                <?php echo htmlspecialchars($me['username'] ?? ''); ?> ▾
            </button>
            <div class="account-dropdown-panel" id="accountDropdown">
                <div class="dropdown-username">👤 <?php echo htmlspecialchars($me['username'] ?? ''); ?></div>
                <div class="dropdown-divider"></div>
                <a href="account.php" class="dropdown-item">🧾 Fiókom</a>
                <?php if ($isAdmin): ?>
                    <a href="admin.php" class="dropdown-item">⚙️ Admin panel</a>
                <?php endif; ?>
                <div class="dropdown-divider"></div>
                <form method="POST">
                    <button type="submit" name="logout" class="logout-form-btn">🚪 Kijelentkezés</button>
                </form>
            </div>
        </div>
    </div>
</nav>

<!-- ══════════════════════════════════════════
     FŐ TARTALOM
     ══════════════════════════════════════════ -->
<div class="page-wrap">

    <?php if ($orderSuccess): ?>
        <div class="success-banner">
            <span class="success-icon">🎉</span>
            <h2>Megkeresés sikeresen elküldve!</h2>
            <p>
                Az eladó(k) üzenetet kaptak a vásárlási szándékodról.<br>
                Keresd meg őket az <a href="uzenetek.php" style="color: var(--orange-bright);">üzenetek</a> oldalon a részletek egyeztetéséhez.
            </p>
        </div>
    <?php endif; ?>

    <?php if (!empty($orderError)): ?>
        <div class="order-error">⚠️ <?php echo htmlspecialchars($orderError); ?></div>
    <?php endif; ?>

    <!-- ═══════════════════
         TERMÉKEK OLDAL
         ═══════════════════ -->
    <div class="items-section">

        <!-- Szűrő sor -->
        <form method="GET" action="vasarlas.php" class="filter-bar">
            <?php if (!empty($search)): ?>
                <input type="hidden" name="q" value="<?php echo htmlspecialchars($search); ?>">
            <?php endif; ?>
            <label>Sorrend:</label>
            <select name="sort" class="filter-select" onchange="this.form.submit()">
                <option value="newest"     <?php echo $sort === 'newest'     ? 'selected' : ''; ?>>Legújabb</option>
                <option value="oldest"     <?php echo $sort === 'oldest'     ? 'selected' : ''; ?>>Legrégebbi</option>
                <option value="price_asc"  <?php echo $sort === 'price_asc'  ? 'selected' : ''; ?>>Ár növekvő</option>
                <option value="price_desc" <?php echo $sort === 'price_desc' ? 'selected' : ''; ?>>Ár csökkenő</option>
            </select>
            <label>Ár:</label>
            <input type="number" name="min_price" class="filter-input" placeholder="Min. Ft"
                   value="<?php echo $minPrice !== null ? (int)$minPrice : ''; ?>" min="0">
            <span style="color: var(--text-muted)">–</span>
            <input type="number" name="max_price" class="filter-input" placeholder="Max. Ft"
                   value="<?php echo $maxPrice !== null ? (int)$maxPrice : ''; ?>" min="0">
            <button type="submit" class="filter-btn">Szűrés</button>
            <?php if (!empty($search) || $minPrice !== null || $maxPrice !== null || $sort !== 'newest'): ?>
                <a href="vasarlas.php" class="filter-btn" style="text-decoration:none;">✕ Visszaállítás</a>
            <?php endif; ?>
            <span class="results-count"><?php echo $totalItems; ?> találat</span>
        </form>

        <!-- Termék rács -->
        <div class="items-grid" id="itemsGrid">
            <?php if (empty($items)): ?>
                <div class="empty-state">
                    <span class="empty-icon">🛍️</span>
                    <h3>Nincs találat</h3>
                    <p>Próbálj más keresési feltételeket!</p>
                </div>
            <?php else: ?>
                <?php foreach ($items as $item):
                    $inCart   = isset($_SESSION['cart'][$item['id']]);
                    $isOwn    = (int)$item['seller_id'] === $userId;
                ?>
                    <div class="item-card <?php echo $inCart ? 'in-cart' : ''; ?>"
                         data-item-id="<?php echo htmlspecialchars($item['id']); ?>">
                        <div class="item-img-wrap">
                            <?php if (!empty($item['primary_image'])): ?>
                                <img src="<?php echo htmlspecialchars($item['primary_image']); ?>"
                                     alt="<?php echo htmlspecialchars($item['title']); ?>"
                                     loading="lazy">
                            <?php else: ?>
                                <div class="item-img-placeholder">📷</div>
                            <?php endif; ?>
                            <?php if ($item['image_count'] > 1): ?>
                                <div class="img-count-badge">📷 <?php echo (int)$item['image_count']; ?></div>
                            <?php endif; ?>
                            <?php if ($inCart): ?>
                                <div class="in-cart-badge">✓ Kosárban</div>
                            <?php endif; ?>
                        </div>
                        <div class="item-body" onclick="openProductModal('<?php echo htmlspecialchars($item['id']); ?>')">
                            <div class="item-title"><?php echo htmlspecialchars($item['title']); ?></div>
                            <div class="item-price"><?php echo number_format($item['price'], 0, ',', ' '); ?> Ft</div>
                            <div class="item-seller">Eladó: <?php echo htmlspecialchars($item['seller_name']); ?></div>
                        </div>
                        <?php if ($isOwn): ?>
                            <button class="item-add-btn own-item" disabled>Saját hirdetés</button>
                        <?php elseif ($inCart): ?>
                            <button class="item-add-btn already-in" disabled>✓ Kosárban van</button>
                        <?php else: ?>
                            <button class="item-add-btn"
                                    onclick="addToCart(event, '<?php echo htmlspecialchars($item['id']); ?>')">
                                🛒 Kosárba
                            </button>
                        <?php endif; ?>
                    </div>
                <?php endforeach; ?>
            <?php endif; ?>
        </div>

        <!-- Lapozó -->
        <?php if ($totalPages > 1): ?>
            <div class="pagination">
                <?php if ($page > 1): ?>
                    <a href="?<?php echo http_build_query(array_merge($_GET, ['page' => $page - 1])); ?>"
                       class="page-btn">← Előző</a>
                <?php endif; ?>
                <?php
                $start = max(1, $page - 2);
                $end   = min($totalPages, $page + 2);
                for ($p = $start; $p <= $end; $p++):
                ?>
                    <a href="?<?php echo http_build_query(array_merge($_GET, ['page' => $p])); ?>"
                       class="page-btn <?php echo $p === $page ? 'current' : ''; ?>">
                        <?php echo $p; ?>
                    </a>
                <?php endfor; ?>
                <?php if ($page < $totalPages): ?>
                    <a href="?<?php echo http_build_query(array_merge($_GET, ['page' => $page + 1])); ?>"
                       class="page-btn">Következő →</a>
                <?php endif; ?>
            </div>
        <?php endif; ?>
    </div>

    <!-- ═══════════════════
         KOSÁR OLDALSÁV
         ═══════════════════ -->
    <aside class="cart-sidebar">
        <div class="cart-title">
            🛒 Kosár
            <span class="cart-count-pill" id="cartCountPill"><?php echo $cartCount; ?></span>
        </div>

        <div class="cart-items-list" id="cartItemsList">
            <?php if (empty($_SESSION['cart'])): ?>
                <div class="cart-empty-msg">
                    <span>🛍️</span>
                    A kosarad üres
                </div>
            <?php else: ?>
                <?php foreach ($_SESSION['cart'] as $cId => $cItem): ?>
                    <div class="cart-item" id="cart-item-<?php echo htmlspecialchars($cId); ?>">
                        <?php if (!empty($cItem['thumb'])): ?>
                            <img src="<?php echo htmlspecialchars($cItem['thumb']); ?>"
                                 class="cart-item-thumb" alt="">
                        <?php else: ?>
                            <div class="cart-item-thumb-placeholder">📷</div>
                        <?php endif; ?>
                        <div class="cart-item-info">
                            <div class="cart-item-name"><?php echo htmlspecialchars($cItem['title']); ?></div>
                            <div class="cart-item-price"><?php echo number_format($cItem['price'], 0, ',', ' '); ?> Ft</div>
                        </div>
                        <button class="cart-item-remove" title="Eltávolítás"
                                onclick="removeFromCart('<?php echo htmlspecialchars($cId); ?>')">✕</button>
                    </div>
                <?php endforeach; ?>
            <?php endif; ?>
        </div>

        <div class="cart-total-row">
            <span class="cart-total-label">Összesen:</span>
            <span class="cart-total-amount" id="cartTotal">
                <?php
                $total = array_sum(array_column($_SESSION['cart'], 'price'));
                echo number_format($total, 0, ',', ' ') . ' Ft';
                ?>
            </span>
        </div>

        <button class="cart-checkout-btn" id="checkoutBtn"
                <?php echo empty($_SESSION['cart']) ? 'disabled' : ''; ?>
                onclick="openCheckout()">
            💳 Megrendelés
        </button>

        <?php if (!empty($_SESSION['cart'])): ?>
            <button class="cart-clear-btn" onclick="clearCart()">🗑️ Kosár ürítése</button>
        <?php endif; ?>
    </aside>
</div>

<!-- ══════════════════════════════════════════
     TERMÉK RÉSZLET MODAL
     ══════════════════════════════════════════ -->
<div class="product-overlay" id="productOverlay">
    <div class="product-modal" id="productModal">
        <button class="pm-close" id="pmClose">✕</button>

        <div class="pm-gallery">
            <img class="pm-main-img" id="pmMainImg" src="" alt="" style="display:none;">
            <div class="pm-no-img" id="pmNoImg">📷</div>
            <button class="pm-nav prev hidden" id="pmPrev">‹</button>
            <button class="pm-nav next hidden" id="pmNext">›</button>
            <div class="pm-thumbs" id="pmThumbs"></div>
        </div>

        <div class="pm-title" id="pmTitle"></div>
        <div class="pm-price" id="pmPrice"></div>
        <div class="pm-meta" id="pmMeta"></div>
        <div class="pm-desc" id="pmDesc"></div>

        <button class="pm-add-btn" id="pmAddBtn">🛒 Kosárba rakás</button>
    </div>
</div>

<!-- ══════════════════════════════════════════
     LIGHTBOX
     ══════════════════════════════════════════ -->
<div class="lightbox-overlay" id="lightboxOverlay">
    <button class="lightbox-close" id="lightboxClose">✕</button>
    <img src="" alt="" id="lightboxImg">
</div>

<!-- ══════════════════════════════════════════
     PÉNZTÁR MODAL
     ══════════════════════════════════════════ -->
<div class="checkout-overlay" id="checkoutOverlay">
    <div class="checkout-modal">
        <h2>💳 Megrendelés leadása</h2>

        <div class="co-order-summary" id="coSummary"></div>

        <form method="POST" action="vasarlas.php" id="checkoutForm">
            <input type="hidden" name="place_order" value="1">

            <div class="co-form-group">
                <label>Teljes név <span>*</span></label>
                <input type="text" name="nev" class="co-input" required
                       placeholder="Kovács Péter"
                       value="<?php echo htmlspecialchars($_POST['nev'] ?? ''); ?>">
            </div>
            <div class="co-form-group">
                <label>Email cím <span>*</span></label>
                <input type="email" name="email" class="co-input" required
                       placeholder="pelda@email.hu"
                       value="<?php echo htmlspecialchars($_POST['email'] ?? ''); ?>">
            </div>
            <div class="co-form-group">
                <label>Cím (utca, házszám) <span>*</span></label>
                <input type="text" name="cim" class="co-input" required
                       placeholder="Fő utca 12."
                       value="<?php echo htmlspecialchars($_POST['cim'] ?? ''); ?>">
            </div>
            <div class="co-row">
                <div class="co-form-group">
                    <label>Város <span>*</span></label>
                    <input type="text" name="varos" class="co-input" required
                           placeholder="Budapest"
                           value="<?php echo htmlspecialchars($_POST['varos'] ?? ''); ?>">
                </div>
                <div class="co-form-group">
                    <label>Irányítószám <span>*</span></label>
                    <input type="text" name="irsz" class="co-input" required
                           placeholder="1234"
                           value="<?php echo htmlspecialchars($_POST['irsz'] ?? ''); ?>">
                </div>
            </div>
            <div class="co-form-group">
                <label>Megjegyzés az eladónak</label>
                <textarea name="megjegyzes" class="co-textarea"
                          placeholder="Opcionális megjegyzés…"><?php echo htmlspecialchars($_POST['megjegyzes'] ?? ''); ?></textarea>
            </div>

            <p class="co-note">
                ℹ️ Ez egy C2C piactér. A megrendelés elküldésekor az eladó(k) üzenetet kapnak az adataidkal, és ők veszik fel veled a kapcsolatot az egyeztetéshez. Az ár és a szállítás módja közvetlenül az eladóval kerül megbeszélésre.
            </p>

            <div class="co-actions">
                <button type="button" class="co-cancel-btn" onclick="closeCheckout()">Mégse</button>
                <button type="submit" class="co-submit-btn">📨 Megrendelés elküldése</button>
            </div>
        </form>
    </div>
</div>

<!-- Toast -->
<div id="cartToast"></div>

<!-- ══════════════════════════════════════════
     JAVASCRIPT
     ══════════════════════════════════════════ -->
<script>
    // ── TÉMA ──
    (function () {
        var t = localStorage.getItem('theme') || 'dark';
        document.documentElement.setAttribute('data-theme', t);
        document.body.setAttribute('data-theme', t);
        var ss = document.getElementById('themeStylesheet');
        if (ss) ss.href = t === 'light' ? 'theme-light.css' : 'theme-dark.css';
    })();

    // ── ACCOUNT DROPDOWN ──
    const accountMenuBtn  = document.getElementById('accountMenuBtn');
    const accountDropdown = document.getElementById('accountDropdown');
    if (accountMenuBtn && accountDropdown) {
        accountMenuBtn.addEventListener('click', e => { e.stopPropagation(); accountDropdown.classList.toggle('show'); });
        accountDropdown.addEventListener('click', e => e.stopPropagation());
        document.addEventListener('click', () => accountDropdown.classList.remove('show'));
    }

    // ── TOAST ──
    let toastTimer = null;
    function showToast(msg, type = 'info') {
        const t = document.getElementById('cartToast');
        t.textContent = msg;
        t.className = 'show ' + type;
        clearTimeout(toastTimer);
        toastTimer = setTimeout(() => { t.className = ''; }, 3200);
    }

    // ── CART STATE ──
    // Szerver-oldali állapotot betöltjük JS-be
    let cartItems = <?php
        $cartJs = [];
        foreach ($_SESSION['cart'] as $id => $ci) {
            $cartJs[$id] = ['id' => $id, 'title' => $ci['title'], 'price' => $ci['price'], 'thumb' => $ci['thumb'] ?? ''];
        }
        echo json_encode($cartJs);
    ?>;

    function recalcCart() {
        const ids   = Object.keys(cartItems);
        const count = ids.length;
        const total = ids.reduce((s, id) => s + (cartItems[id].price || 0), 0);

        // Badge a topbarban
        document.querySelectorAll('.cart-badge').forEach(el => {
            el.textContent = count > 9 ? '9+' : count;
            el.style.display = count > 0 ? 'inline-flex' : 'none';
        });

        // Pill a sávban
        const pill = document.getElementById('cartCountPill');
        if (pill) pill.textContent = count;

        // Összeg
        const tot = document.getElementById('cartTotal');
        if (tot) tot.textContent = total.toLocaleString('hu-HU') + ' Ft';

        // Checkout gomb
        const btn = document.getElementById('checkoutBtn');
        if (btn) btn.disabled = count === 0;
    }

    function renderCartList() {
        const list = document.getElementById('cartItemsList');
        if (!list) return;
        const ids = Object.keys(cartItems);
        if (ids.length === 0) {
            list.innerHTML = '<div class="cart-empty-msg"><span>🛍️</span>A kosarad üres</div>';
            // Kosár ürítés gomb eltüntetése
            const clearBtn = document.querySelector('.cart-clear-btn');
            if (clearBtn) clearBtn.remove();
            return;
        }
        list.innerHTML = ids.map(id => {
            const ci = cartItems[id];
            const thumb = ci.thumb
                ? `<img src="${escHtml(ci.thumb)}" class="cart-item-thumb" alt="">`
                : `<div class="cart-item-thumb-placeholder">📷</div>`;
            return `<div class="cart-item" id="cart-item-${escHtml(id)}">
                ${thumb}
                <div class="cart-item-info">
                    <div class="cart-item-name">${escHtml(ci.title)}</div>
                    <div class="cart-item-price">${ci.price.toLocaleString('hu-HU')} Ft</div>
                </div>
                <button class="cart-item-remove" title="Eltávolítás" onclick="removeFromCart('${escHtml(id)}')">✕</button>
            </div>`;
        }).join('');
    }

    function refreshCardStates() {
        document.querySelectorAll('.item-card[data-item-id]').forEach(card => {
            const id = card.dataset.itemId;
            const inCart = !!cartItems[id];
            card.classList.toggle('in-cart', inCart);
            // badge
            const badge = card.querySelector('.in-cart-badge');
            const addBtn = card.querySelector('.item-add-btn');
            if (badge) badge.style.display = inCart ? 'block' : 'none';
            if (addBtn && !addBtn.classList.contains('own-item')) {
                if (inCart) {
                    addBtn.textContent = '✓ Kosárban van';
                    addBtn.classList.add('already-in');
                    addBtn.disabled = true;
                } else {
                    addBtn.textContent = '🛒 Kosárba';
                    addBtn.classList.remove('already-in');
                    addBtn.disabled = false;
                    addBtn.onclick = (e) => addToCart(e, id);
                }
            }
        });
    }

    // ── KOSÁRBA ADÁS ──
    async function addToCart(e, itemId) {
        e.stopPropagation();
        try {
            const fd = new FormData();
            fd.append('add_to_cart', '1');
            fd.append('item_id', itemId);
            const res  = await fetch('vasarlas.php', { method: 'POST', body: fd });
            const data = await res.json();
            if (data.success) {
                // Termék adatainak lekérése a kosárhoz
                const itemRes  = await fetch(`vasarlas.php?get_item=${encodeURIComponent(itemId)}`);
                const itemData = await itemRes.json();
                if (!itemData.error) {
                    cartItems[itemId] = {
                        id: itemData.id, title: itemData.title,
                        price: parseFloat(itemData.price),
                        thumb: itemData.images && itemData.images.length > 0 ? itemData.images[0] : ''
                    };
                }
                showToast('✓ ' + data.message, 'success');
                renderCartList();
                recalcCart();
                refreshCardStates();
                updateCheckoutSummary();
                // Kosár ürítés gomb megjelenítése
                if (!document.querySelector('.cart-clear-btn')) {
                    const sidebar = document.querySelector('.cart-sidebar');
                    if (sidebar) {
                        const cb = document.createElement('button');
                        cb.className = 'cart-clear-btn';
                        cb.textContent = '🗑️ Kosár ürítése';
                        cb.onclick = clearCart;
                        sidebar.appendChild(cb);
                    }
                }
            } else {
                showToast('⚠️ ' + data.message, 'error');
            }
        } catch {
            showToast('⚠️ Hálózati hiba történt.', 'error');
        }
    }

    // ── KOSÁRBÓL ELTÁVOLÍTÁS ──
    async function removeFromCart(itemId) {
        try {
            const fd = new FormData();
            fd.append('remove_from_cart', '1');
            fd.append('item_id', itemId);
            const res  = await fetch('vasarlas.php', { method: 'POST', body: fd });
            const data = await res.json();
            if (data.success) {
                delete cartItems[itemId];
                showToast('Termék eltávolítva a kosárból.', 'info');
                renderCartList();
                recalcCart();
                refreshCardStates();
                updateCheckoutSummary();
                // product modal gomb frissítése ha nyitva van
                const pmAddBtn = document.getElementById('pmAddBtn');
                if (pmAddBtn && pmAddBtn.dataset.itemId === itemId) {
                    pmAddBtn.textContent = '🛒 Kosárba rakás';
                    pmAddBtn.classList.remove('already-in');
                    pmAddBtn.disabled = false;
                }
            }
        } catch {
            showToast('⚠️ Hálózati hiba.', 'error');
        }
    }

    // ── KOSÁR TÖRLÉSE ──
    async function clearCart() {
        if (!confirm('Biztosan üríted a kosarat?')) return;
        try {
            const fd = new FormData();
            fd.append('clear_cart', '1');
            await fetch('vasarlas.php', { method: 'POST', body: fd });
            cartItems = {};
            showToast('Kosár kiürítve.', 'info');
            renderCartList();
            recalcCart();
            refreshCardStates();
            updateCheckoutSummary();
        } catch {
            showToast('⚠️ Hálózati hiba.', 'error');
        }
    }

    // ── PÉNZTÁR MODAL ──
    function updateCheckoutSummary() {
        const div = document.getElementById('coSummary');
        if (!div) return;
        const ids = Object.keys(cartItems);
        if (ids.length === 0) { div.innerHTML = '<p style="color:var(--text-muted);text-align:center;font-size:.9rem;">A kosár üres.</p>'; return; }
        const total = ids.reduce((s, id) => s + (cartItems[id].price || 0), 0);
        div.innerHTML = `<h3>Rendelt termékek</h3>` +
            ids.map(id => {
                const ci = cartItems[id];
                return `<div class="co-summary-line">
                    <span class="co-summary-name">${escHtml(ci.title)}</span>
                    <span>${ci.price.toLocaleString('hu-HU')} Ft</span>
                </div>`;
            }).join('') +
            `<div class="co-summary-line"><span>Összesen</span><span>${total.toLocaleString('hu-HU')} Ft</span></div>`;
    }

    function openCheckout() {
        if (Object.keys(cartItems).length === 0) {
            showToast('A kosarad üres!', 'error');
            return;
        }
        updateCheckoutSummary();
        document.getElementById('checkoutOverlay').classList.add('active');
        document.body.style.overflow = 'hidden';
    }

    function closeCheckout() {
        document.getElementById('checkoutOverlay').classList.remove('active');
        document.body.style.overflow = '';
    }

    document.getElementById('checkoutOverlay').addEventListener('click', e => {
        if (e.target === document.getElementById('checkoutOverlay')) closeCheckout();
    });

    // ── TERMÉK MODAL ──
    let pmImages    = [];
    let pmImgIdx    = 0;
    let pmCurrentId = null;
    let pmOwnItem   = false;

    const productOverlay = document.getElementById('productOverlay');
    const pmMainImg      = document.getElementById('pmMainImg');
    const pmNoImg        = document.getElementById('pmNoImg');
    const pmThumbs       = document.getElementById('pmThumbs');
    const pmPrev         = document.getElementById('pmPrev');
    const pmNext         = document.getElementById('pmNext');
    const pmAddBtn       = document.getElementById('pmAddBtn');

    function pmSetImage(idx) {
        if (pmImages.length === 0 || idx < 0 || idx >= pmImages.length) return;
        pmImgIdx = idx;
        pmMainImg.src = pmImages[idx];
        pmMainImg.style.display = 'block';
        pmNoImg.style.display = 'none';
        pmThumbs.querySelectorAll('.pm-thumb').forEach((th, i) => th.classList.toggle('active', i === idx));
    }

    async function openProductModal(itemId) {
        try {
            const res  = await fetch(`vasarlas.php?get_item=${encodeURIComponent(itemId)}`);
            const item = await res.json();
            if (item.error) { showToast('⚠️ ' + item.error, 'error'); return; }

            pmCurrentId = item.id;
            pmOwnItem   = (parseInt(item.seller_id) === <?php echo (int)$_SESSION['user_id']; ?>);
            pmImages    = item.images || [];
            pmImgIdx    = 0;

            document.getElementById('pmTitle').textContent = item.title;
            document.getElementById('pmPrice').textContent = Number(item.price).toLocaleString('hu-HU') + ' Ft';
            document.getElementById('pmMeta').textContent  =
                'Eladó: ' + item.seller_name + '  •  ' + (item.created_at || '').substring(0, 10);
            document.getElementById('pmDesc').textContent  = item.description || '';

            // Kép galéria
            pmThumbs.innerHTML = '';
            if (pmImages.length > 0) {
                pmImages.forEach((src, i) => {
                    const th = document.createElement('div');
                    th.className = 'pm-thumb' + (i === 0 ? ' active' : '');
                    th.innerHTML = `<img src="${escHtml(src)}" alt="">`;
                    th.addEventListener('click', () => pmSetImage(i));
                    pmThumbs.appendChild(th);
                });
                pmSetImage(0);
            } else {
                pmMainImg.style.display = 'none';
                pmNoImg.style.display   = 'flex';
            }

            pmPrev.classList.toggle('hidden', pmImages.length <= 1);
            pmNext.classList.toggle('hidden', pmImages.length <= 1);

            // Kosárba gomb állapota
            pmAddBtn.dataset.itemId = item.id;
            if (pmOwnItem) {
                pmAddBtn.textContent = 'Saját hirdetésed';
                pmAddBtn.className   = 'pm-add-btn own-item';
                pmAddBtn.disabled    = true;
                pmAddBtn.onclick     = null;
            } else if (cartItems[item.id]) {
                pmAddBtn.textContent = '✓ Már a kosárban';
                pmAddBtn.className   = 'pm-add-btn already-in';
                pmAddBtn.disabled    = true;
                pmAddBtn.onclick     = null;
            } else {
                pmAddBtn.textContent = '🛒 Kosárba rakás';
                pmAddBtn.className   = 'pm-add-btn';
                pmAddBtn.disabled    = false;
                pmAddBtn.onclick     = async (e) => {
                    await addToCart(e, item.id);
                    // Gomb frissítése
                    if (cartItems[item.id]) {
                        pmAddBtn.textContent = '✓ Már a kosárban';
                        pmAddBtn.classList.add('already-in');
                        pmAddBtn.disabled = true;
                    }
                };
            }

            productOverlay.classList.add('active');
            document.body.style.overflow = 'hidden';
        } catch {
            showToast('⚠️ Nem sikerült betölteni a terméket.', 'error');
        }
    }

    function closeProductModal() {
        productOverlay.classList.remove('active');
        document.body.style.overflow = '';
    }

    document.getElementById('pmClose').addEventListener('click', closeProductModal);
    productOverlay.addEventListener('click', e => { if (e.target === productOverlay) closeProductModal(); });

    pmPrev.addEventListener('click', e => { e.stopPropagation(); pmSetImage(pmImgIdx > 0 ? pmImgIdx - 1 : pmImages.length - 1); });
    pmNext.addEventListener('click', e => { e.stopPropagation(); pmSetImage(pmImgIdx < pmImages.length - 1 ? pmImgIdx + 1 : 0); });

    pmMainImg.addEventListener('click', () => {
        if (pmMainImg.src) {
            document.getElementById('lightboxImg').src = pmMainImg.src;
            document.getElementById('lightboxOverlay').classList.add('active');
        }
    });

    // ── LIGHTBOX ──
    const lightboxOverlay = document.getElementById('lightboxOverlay');
    document.getElementById('lightboxClose').addEventListener('click', () => lightboxOverlay.classList.remove('active'));
    lightboxOverlay.addEventListener('click', e => { if (e.target === lightboxOverlay) lightboxOverlay.classList.remove('active'); });

    // ── ESC BILLENTYŰ ──
    document.addEventListener('keydown', e => {
        if (e.key === 'Escape') {
            lightboxOverlay.classList.remove('active');
            if (productOverlay.classList.contains('active'))  closeProductModal();
            if (document.getElementById('checkoutOverlay').classList.contains('active')) closeCheckout();
        }
    });

    // ── UNREAD ÜZENETEK POLLING ──
    let lastUnread   = <?php echo $unreadMsgCount; ?>;
    const msgBadge   = document.getElementById('msgBadge');

    async function checkUnread() {
        try {
            const res  = await fetch('vasarlas.php?get_unread_count=1');
            const data = await res.json();
            if (data.error) return;
            const n = data.unread_count;
            if (msgBadge) {
                msgBadge.textContent = n > 9 ? '9+' : n;
                msgBadge.classList.toggle('hidden', n === 0);
            }
            if (n > lastUnread && data.last_message) {
                showToast(`💬 Új üzenet: ${data.last_message.sender} — "${data.last_message.preview}"`, 'info');
            }
            lastUnread = n;
        } catch {}
    }

    let pollInterval = setInterval(checkUnread, 15000);
    document.addEventListener('visibilitychange', () => {
        clearInterval(pollInterval);
        pollInterval = setInterval(checkUnread, document.hidden ? 60000 : 15000);
        if (!document.hidden) checkUnread();
    });

    // ── SEGÉDFÜGGVÉNY ──
    function escHtml(str) {
        if (!str) return '';
        return String(str).replace(/[&<>"']/g, m =>
            ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[m])
        );
    }

    // ── INICIALIZÁLÁS ──
    recalcCart();
    updateCheckoutSummary();

    <?php if ($orderSuccess): ?>
        window.scrollTo({ top: 0, behavior: 'smooth' });
    <?php endif; ?>
</script>

</body>
</html>