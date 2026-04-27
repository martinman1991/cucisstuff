<?php
session_start();

// Hibák megjelenítése (fejlesztéshez)
ini_set('display_errors', 1);
ini_set('display_startup_errors', 1);
error_reporting(E_ALL);

// =============================================
// 1. KIJELENTKEZÉS
// =============================================
if (isset($_POST['logout'])) {
    $_SESSION = array();
    session_destroy();
    header("Location: index.php");
    exit();
}

// =============================================
// 2. BEJELENTKEZÉS ELLENŐRZÉS
// =============================================
if (!isset($_SESSION['logged_in']) || $_SESSION['logged_in'] !== true) {
    if (isset($_GET['search_query']) || isset($_GET['get_item']) || isset($_GET['get_seller']) || isset($_GET['get_unread_count'])) {
        header('Content-Type: application/json');
        echo json_encode(['error' => 'Nincs bejelentkezve']);
        exit();
    }
    header("Location: index.php");
    exit();
}

// Database connection
require_once 'config.php';

// === KÉP ÁTMÉRETEZŐ FÜGGVÉNY ===
function resizeImage($source, $destination, $maxDim = 1024)
{
    $info = getimagesize($source);
    if (!$info) return false;

    $mime = $info['mime'];
    $srcWidth = $info[0];
    $srcHeight = $info[1];

    if ($srcWidth <= $maxDim && $srcHeight <= $maxDim) {
        return copy($source, $destination);
    }

    $ratio = $srcWidth / $srcHeight;
    if ($srcWidth > $srcHeight) {
        $newWidth = $maxDim;
        $newHeight = (int) round($maxDim / $ratio);
    } else {
        $newHeight = $maxDim;
        $newWidth = (int) round($maxDim * $ratio);
    }

    switch ($mime) {
        case 'image/jpeg':
            $srcImg = imagecreatefromjpeg($source);
            break;
        case 'image/png':
            $srcImg = imagecreatefrompng($source);
            break;
        case 'image/gif':
            $srcImg = imagecreatefromgif($source);
            break;
        case 'image/webp':
            if (function_exists('imagecreatefromwebp')) $srcImg = imagecreatefromwebp($source);
            else return copy($source, $destination);
            break;
        default:
            return false;
    }
    if (!$srcImg) return false;

    $dstImg = imagecreatetruecolor($newWidth, $newHeight);
    if ($mime == 'image/png' || $mime == 'image/webp') {
        imagealphablending($dstImg, false);
        imagesavealpha($dstImg, true);
        $transparent = imagecolorallocatealpha($dstImg, 0, 0, 0, 127);
        imagefilledrectangle($dstImg, 0, 0, $newWidth, $newHeight, $transparent);
    } elseif ($mime == 'image/gif') {
        $transparentIndex = imagecolortransparent($srcImg);
        if ($transparentIndex >= 0) {
            $transparentColor = imagecolorsforindex($srcImg, $transparentIndex);
            $transparentIndex = imagecolorallocate($dstImg, $transparentColor['red'], $transparentColor['green'], $transparentColor['blue']);
            imagefill($dstImg, 0, 0, $transparentIndex);
            imagecolortransparent($dstImg, $transparentIndex);
        }
    }
    imagecopyresampled($dstImg, $srcImg, 0, 0, 0, 0, $newWidth, $newHeight, $srcWidth, $srcHeight);

    $success = false;
    switch ($mime) {
        case 'image/jpeg':
            $success = imagejpeg($dstImg, $destination, 85);
            break;
        case 'image/png':
            $success = imagepng($dstImg, $destination, 8);
            break;
        case 'image/gif':
            $success = imagegif($dstImg, $destination);
            break;
        case 'image/webp':
            if (function_exists('imagewebp')) $success = imagewebp($dstImg, $destination, 85);
            break;
    }
    imagedestroy($srcImg);
    imagedestroy($dstImg);
    return $success;
}

$uploadError = $_SESSION['upload_error'] ?? '';
$formData = $_SESSION['form_data'] ?? [];
unset($_SESSION['upload_error'], $_SESSION['form_data']);

try {
    $conn = new PDO("mysql:host=" . DB_HOST . ";dbname=" . DB_NAME, DB_USER, DB_PASS);
    $conn->setAttribute(PDO::ATTR_ERRMODE, PDO::ERRMODE_EXCEPTION);

    $isAdmin = false;
    if (isset($_SESSION['user_id'])) {
        $adminCheck = $conn->prepare("SELECT COUNT(*) FROM admins WHERE user_id = ?");
        $adminCheck->execute([$_SESSION['user_id']]);
        $isAdmin = $adminCheck->fetchColumn() > 0;
    }

    // GET UNREAD MESSAGES COUNT (JSON)
    if (isset($_GET['get_unread_count'])) {
        header('Content-Type: application/json');
        try {
            $unreadStmt = $conn->prepare("SELECT COUNT(*) FROM uzenetek WHERE receiver_id = ? AND is_read = 0");
            $unreadStmt->execute([$_SESSION['user_id']]);
            $unreadCount = (int)$unreadStmt->fetchColumn();

            $lastMsgStmt = $conn->prepare("
                SELECT u.username AS sender_name, m.message, m.sent_at
                FROM uzenetek m
                JOIN users u ON m.sender_id = u.id
                WHERE m.receiver_id = ? AND m.is_read = 0
                ORDER BY m.sent_at DESC
                LIMIT 1
            ");
            $lastMsgStmt->execute([$_SESSION['user_id']]);
            $lastMsg = $lastMsgStmt->fetch(PDO::FETCH_ASSOC);

            echo json_encode([
                'unread_count' => $unreadCount,
                'last_message' => $lastMsg ? [
                    'sender' => $lastMsg['sender_name'],
                    'preview' => mb_substr($lastMsg['message'], 0, 50) . (mb_strlen($lastMsg['message']) > 50 ? '…' : ''),
                    'sent_at' => $lastMsg['sent_at']
                ] : null
            ]);
        } catch (PDOException $e) {
            echo json_encode(['error' => 'Adatbázis hiba: ' . $e->getMessage()]);
        }
        exit;
    }

    // SEARCH HANDLER (JSON)
    if (isset($_GET['search_query']) && strlen($_GET['search_query']) >= 2) {
        header('Content-Type: application/json');
        try {
            $query = '%' . $_GET['search_query'] . '%';
            $stmt = $conn->prepare("
                SELECT 
                    i.id, i.title, i.price, u.username as seller_name,
                    (SELECT image_path FROM item_images WHERE item_id = i.id AND is_primary = 1 LIMIT 1) as primary_image
                FROM items i
                JOIN users u ON i.user_id = u.id
                WHERE i.title LIKE :q OR i.description LIKE :q
                ORDER BY i.created_at DESC
                LIMIT 10
            ");
            $stmt->execute([':q' => $query]);
            $results = $stmt->fetchAll(PDO::FETCH_ASSOC);
            echo json_encode($results);
        } catch (PDOException $e) {
            echo json_encode(['error' => 'Adatbázis hiba: ' . $e->getMessage()]);
        }
        exit;
    }

    // GET ITEM DETAILS (JSON)
    if (isset($_GET['get_item']) && !empty($_GET['get_item'])) {
        header('Content-Type: application/json');
        try {
            $itemId = $_GET['get_item'];
            $stmt = $conn->prepare("
                SELECT i.id, i.title, i.description, i.price, i.created_at, u.username as seller_name, i.user_id
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
        } catch (PDOException $e) {
            echo json_encode(['error' => 'Adatbázis hiba: ' . $e->getMessage()]);
        }
        exit;
    }

    // GET SELLER PROFILE (JSON) – most már minden termékkel
    if (isset($_GET['get_seller']) && !empty($_GET['get_seller'])) {
        header('Content-Type: application/json');
        try {
            $sellerId = (int)$_GET['get_seller'];

            $sellerStmt = $conn->prepare("
                SELECT u.id, u.username, u.created_at, u.profile_picture,
                       COUNT(DISTINCT i.id) AS item_count,
                       (SELECT COUNT(*) FROM admins WHERE user_id = u.id) AS is_admin
                FROM users u
                LEFT JOIN items i ON i.user_id = u.id
                WHERE u.id = ?
                GROUP BY u.id, u.username, u.created_at, u.profile_picture
            ");
            $sellerStmt->execute([$sellerId]);
            $seller = $sellerStmt->fetch(PDO::FETCH_ASSOC);
            if (!$seller) {
                echo json_encode(['error' => 'Felhasználó nem található']);
                exit;
            }

            // MINDEN termék lekérése (LIMIT eltávolítva)
            $latestStmt = $conn->prepare("
                SELECT i.id, i.title, i.price,
                       (SELECT image_path FROM item_images WHERE item_id = i.id AND is_primary = 1 LIMIT 1) as thumb
                FROM items i
                WHERE i.user_id = ?
                ORDER BY i.created_at DESC
            ");
            $latestStmt->execute([$sellerId]);
            $seller['latest_items'] = $latestStmt->fetchAll(PDO::FETCH_ASSOC);

            echo json_encode($seller);
        } catch (PDOException $e) {
            echo json_encode(['error' => 'Adatbázis hiba: ' . $e->getMessage()]);
        }
        exit;
    }

    // TERMÉK FELTÖLTÉS
    if ($_SERVER['REQUEST_METHOD'] === 'POST' && isset($_POST['upload_item'])) {
        $title       = trim($_POST['item_title'] ?? '');
        $description = trim($_POST['item_description'] ?? '');
        $price       = trim($_POST['item_price'] ?? '');

        if (!isset($_FILES['item_images']) || empty($_FILES['item_images']['name'][0]) || $_FILES['item_images']['error'][0] === UPLOAD_ERR_NO_FILE) {
            $_SESSION['upload_error'] = 'Legalább egy képet fel kell tölteni!';
            $_SESSION['form_data'] = compact('title', 'description', 'price');
            header("Location: main.php");
            exit();
        } elseif ($title === '' || $description === '' || $price === '') {
            $_SESSION['upload_error'] = 'Minden mező kitöltése kötelező!';
            $_SESSION['form_data'] = compact('title', 'description', 'price');
            header("Location: main.php");
            exit();
        } elseif (!is_numeric($price) || floatval($price) < 0) {
            $_SESSION['upload_error'] = 'Az ár csak pozitív szám lehet!';
            $_SESSION['form_data'] = compact('title', 'description', 'price');
            header("Location: main.php");
            exit();
        } else {
            $allowedTypes = ['image/jpeg', 'image/png', 'image/gif', 'image/webp'];
            $maxFileSize = 5 * 1024 * 1024;
            $files = $_FILES['item_images'];
            $phpFileErrors = [
                UPLOAD_ERR_OK => 'Sikeres feltöltés.',
                UPLOAD_ERR_INI_SIZE => 'A fájl mérete meghaladja a szerver által engedélyezett maximumot.',
                UPLOAD_ERR_FORM_SIZE => 'A fájl mérete meghaladja az űrlap által engedélyezett maximumot.',
                UPLOAD_ERR_PARTIAL => 'A fájl csak részben lett feltöltve.',
                UPLOAD_ERR_NO_FILE => 'Nem lett fájl feltöltve.',
                UPLOAD_ERR_NO_TMP_DIR => 'Hiányzik az ideiglenes könyvtár.',
                UPLOAD_ERR_CANT_WRITE => 'A fájl írása sikertelen.',
                UPLOAD_ERR_EXTENSION => 'Egy PHP kiterjesztés leállította a feltöltést.',
            ];

            for ($i = 0; $i < count($files['name']); $i++) {
                if ($files['error'][$i] !== UPLOAD_ERR_OK) {
                    $errCode = $files['error'][$i];
                    $errMsg = $phpFileErrors[$errCode] ?? "Ismeretlen hibakód: $errCode";
                    $_SESSION['upload_error'] = "Hiba a(z) {$files['name'][$i]} feltöltésekor: $errMsg";
                    $_SESSION['form_data'] = compact('title', 'description', 'price');
                    header("Location: main.php");
                    exit();
                }
                if (!in_array($files['type'][$i], $allowedTypes)) {
                    $_SESSION['upload_error'] = 'Csak JPEG, PNG, GIF és WebP formátumú képek tölthetők fel!';
                    $_SESSION['form_data'] = compact('title', 'description', 'price');
                    header("Location: main.php");
                    exit();
                }
                if ($files['size'][$i] > $maxFileSize) {
                    $_SESSION['upload_error'] = 'Egy kép maximális mérete 5MB lehet!';
                    $_SESSION['form_data'] = compact('title', 'description', 'price');
                    header("Location: main.php");
                    exit();
                }
            }

            do {
                $newId = substr(str_shuffle('abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789'), 0, 12);
                $check = $conn->prepare("SELECT COUNT(*) FROM items WHERE id = ?");
                $check->execute([$newId]);
            } while ($check->fetchColumn() > 0);

            $conn->beginTransaction();
            try {
                $insert = $conn->prepare("
                    INSERT INTO items (id, user_id, title, description, price)
                    VALUES (:id, :user_id, :title, :description, :price)
                ");
                $insert->execute([
                    ':id' => $newId,
                    ':user_id' => $_SESSION['user_id'],
                    ':title' => $title,
                    ':description' => $description,
                    ':price' => floatval($price),
                ]);

                $uploadDir = 'uploads/' . $newId . '/';
                if (!file_exists($uploadDir)) {
                    if (!mkdir($uploadDir, 0777, true) && !is_dir($uploadDir)) {
                        throw new Exception('Nem sikerült létrehozni a könyvtárat: ' . $uploadDir);
                    }
                }

                $sortOrder = 0;
                for ($i = 0; $i < count($files['name']); $i++) {
                    $extension = pathinfo($files['name'][$i], PATHINFO_EXTENSION);
                    $filename = uniqid() . '_' . $i . '.' . $extension;
                    $filepath = $uploadDir . $filename;

                    if (!resizeImage($files['tmp_name'][$i], $filepath, 1024)) {
                        $lastError = error_get_last();
                        throw new Exception(
                            'Nem sikerült átméretezni/elmenteni a fájlt: ' . $files['name'][$i] .
                                ' ide: ' . $filepath .
                                ($lastError ? ' - Hiba: ' . $lastError['message'] : '')
                        );
                    }

                    $imageInsert = $conn->prepare("
                        INSERT INTO item_images (item_id, image_path, image_filename, is_primary, sort_order)
                        VALUES (:item_id, :image_path, :image_filename, :is_primary, :sort_order)
                    ");
                    $imageInsert->execute([
                        ':item_id' => $newId,
                        ':image_path' => $filepath,
                        ':image_filename' => $filename,
                        ':is_primary' => ($i === 0) ? 1 : 0,
                        ':sort_order' => $sortOrder
                    ]);
                    $sortOrder++;
                }

                $conn->commit();
                header("Location: main.php?upload=success");
                exit();
            } catch (Exception $e) {
                $conn->rollBack();
                $_SESSION['upload_error'] = 'Hiba történt a hirdetés mentése során: ' . $e->getMessage();
                $_SESSION['form_data'] = compact('title', 'description', 'price');
                header("Location: main.php");
                exit();
            }
        }
    }

    // Handle item update
    if ($_SERVER['REQUEST_METHOD'] === 'POST' && isset($_POST['edit_item'])) {
        $itemId  = $_POST['item_id'] ?? '';
        $title   = trim($_POST['edit_title'] ?? '');
        $desc    = trim($_POST['edit_description'] ?? '');
        $price   = trim($_POST['edit_price'] ?? '');

        $ownerCheck = $conn->prepare("SELECT user_id FROM items WHERE id = ?");
        $ownerCheck->execute([$itemId]);
        $ownerRow = $ownerCheck->fetch(PDO::FETCH_ASSOC);

        $canEdit = $itemId && $ownerRow && ($isAdmin || (isset($_SESSION['user_id']) && $ownerRow['user_id'] == $_SESSION['user_id']));

        if ($canEdit && $title !== '' && $desc !== '' && is_numeric($price) && floatval($price) >= 0) {
            try {
                $upd = $conn->prepare("UPDATE items SET title=:title, description=:desc, price=:price WHERE id=:id");
                $upd->execute([':title' => $title, ':desc' => $desc, ':price' => floatval($price), ':id' => $itemId]);
                header("Location: main.php?edit=success");
                exit();
            } catch (Exception $e) {
                $uploadError = 'Hiba a módosítás során: ' . $e->getMessage();
            }
        } else {
            $uploadError = 'Érvénytelen adatok vagy nincs jogosultság!';
        }
    }

    // Handle item deletion
    if ($_SERVER['REQUEST_METHOD'] === 'POST' && isset($_POST['delete_item'])) {
        $itemId = $_POST['item_id'] ?? '';
        $ownerCheck2 = $conn->prepare("SELECT user_id FROM items WHERE id = ?");
        $ownerCheck2->execute([$itemId]);
        $ownerRow2 = $ownerCheck2->fetch(PDO::FETCH_ASSOC);
        $canDelete = $itemId && $ownerRow2 && ($isAdmin || (isset($_SESSION['user_id']) && $ownerRow2['user_id'] == $_SESSION['user_id']));

        if ($canDelete) {
            try {
                $imageStmt = $conn->prepare("SELECT image_path FROM item_images WHERE item_id = ?");
                $imageStmt->execute([$itemId]);
                $images = $imageStmt->fetchAll(PDO::FETCH_ASSOC);
                foreach ($images as $image) {
                    if (file_exists($image['image_path'])) unlink($image['image_path']);
                }
                $itemDir = 'uploads/' . $itemId . '/';
                if (is_dir($itemDir)) rmdir($itemDir);

                $deleteStmt = $conn->prepare("DELETE FROM items WHERE id = ?");
                $deleteStmt->execute([$itemId]);

                if ($isAdmin) header("Location: main.php");
                else header("Location: main.php?deleted=1");
                exit();
            } catch (Exception $e) {
                $uploadError = 'Hiba történt a törlés során: ' . $e->getMessage();
            }
        }
    }

    // Handle item report
    if ($_SERVER['REQUEST_METHOD'] === 'POST' && isset($_POST['report_item'])) {
        $itemId = $_POST['item_id'] ?? '';
        $reason = trim($_POST['report_reason'] ?? '');
        if ($itemId && $reason) {
            try {
                $reportStmt = $conn->prepare("
                    INSERT INTO reports (item_id, user_id, reason, created_at)
                    VALUES (?, ?, ?, NOW())
                ");
                $reportStmt->execute([$itemId, $_SESSION['user_id'], $reason]);
                $reportSuccess = true;
            } catch (Exception $e) {
                $reportError = 'Hiba történt a bejelentés során: ' . $e->getMessage();
            }
        }
    }

    // Pagination settings
    $itemsPerPage = 24;
    $page = isset($_GET['page']) ? max(1, intval($_GET['page'])) : 1;
    $offset = ($page - 1) * $itemsPerPage;

    $totalStmt = $conn->query("SELECT COUNT(*) FROM items");
    $totalItems = $totalStmt->fetchColumn();
    $totalPages = ceil($totalItems / $itemsPerPage);

    $referer = $_SERVER['HTTP_REFERER'] ?? '';
    $comingFromPagination = strpos($referer, 'main.php') !== false;
    if (!isset($_SESSION['items_seed']) || !$comingFromPagination) {
        $_SESSION['items_seed'] = mt_rand(1, 999999);
    }
    $seed = $_SESSION['items_seed'];

    $stmt = $conn->prepare("
        SELECT i.*, u.username as seller_name
        FROM items i
        JOIN users u ON i.user_id = u.id
        ORDER BY RAND(:seed)
        LIMIT :offset, :itemsPerPage
    ");
    $stmt->bindParam(':seed', $seed, PDO::PARAM_INT);
    $stmt->bindParam(':offset', $offset, PDO::PARAM_INT);
    $stmt->bindParam(':itemsPerPage', $itemsPerPage, PDO::PARAM_INT);
    $stmt->execute();
    $items = $stmt->fetchAll(PDO::FETCH_ASSOC);
} catch (PDOException $e) {
    echo "Connection failed: " . $e->getMessage();
    $items = [];
    $totalPages = 0;
    $page = 1;
}

function formatMessage($msg)
{
    $msg = htmlspecialchars($msg);
    $msg = preg_replace('/\*(.*?)\*/', '<strong>$1</strong>', $msg);
    $msg = preg_replace('/\-(.*?)\-/', '<em>$1</em>', $msg);
    return $msg;
}

$unreadMsgCount = 0;
try {
    $unreadStmt = $conn->prepare("SELECT COUNT(*) FROM uzenetek WHERE receiver_id = ? AND is_read = 0");
    $unreadStmt->execute([$_SESSION['user_id']]);
    $unreadMsgCount = (int)$unreadStmt->fetchColumn();
} catch (Exception $e) {
    $unreadMsgCount = 0;
}
?>
<!DOCTYPE html>
<html lang="hu">

<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0, maximum-scale=5.0, user-scalable=yes">
    <title>Főoldal - Termékek</title>
    <link rel="stylesheet" href="styles.css">
    <link rel="stylesheet" id="themeStylesheet" href="theme-dark.css?v=2">
    <link rel="icon" type="image/png" href="logo.png">
    <style>
        * {
            box-sizing: border-box;
            margin: 0;
            padding: 0;
        }

        :root {
            --orange-bright: #ff8c00;
            --orange-glow: rgba(255, 140, 0, 0.3);
            --glass-bg: rgba(0, 0, 0, 0.7);
            --glass-border: rgba(255, 140, 0, 0.2);
            --text-primary: #ffffff;
            --shadow-deep: 0 10px 30px rgba(0, 0, 0, 0.5);
            --shadow-orange: 0 0 20px rgba(255, 140, 0, 0.2);
            --placeholder-bg: rgba(255, 140, 0, 0.1);
            --placeholder-text: rgba(255, 140, 0, 0.7);
        }

        body {
            min-height: 100vh;
            width: 100%;
            margin: 0;
            padding: 0;
            background: #0a0a0a;
            color: var(--text-primary);
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Oxygen, Ubuntu, Cantarell, sans-serif;
            position: relative;
            overflow-x: hidden;
            display: block;
        }

        .noise {
            position: fixed;
            top: -50%;
            left: -50%;
            right: -50%;
            bottom: -50%;
            width: 200%;
            height: 200%;
            background: transparent url('data:image/svg+xml,%3Csvg viewBox="0 0 200 200" xmlns="http://www.w3.org/2000/svg"%3E%3Cfilter id="noise"%3E%3CfeTurbulence type="fractalNoise" baseFrequency="0.65" numOctaves="3" stitchTiles="stitch"/%3E%3C/filter%3E%3Crect width="100%25" height="100%25" filter="url(%23noise)" opacity="0.08"/%3E%3C/svg%3E') repeat;
            pointer-events: none;
            z-index: -1;
            animation: noise 0.2s infinite;
            opacity: 0.4;
        }

        @keyframes noise {

            0%,
            100% {
                transform: translate(0, 0)
            }

            10% {
                transform: translate(-5%, -5%)
            }

            20% {
                transform: translate(-10%, 5%)
            }

            30% {
                transform: translate(5%, -10%)
            }

            40% {
                transform: translate(-5%, 15%)
            }

            50% {
                transform: translate(-10%, 5%)
            }

            60% {
                transform: translate(15%, 0)
            }

            70% {
                transform: translate(0, 10%)
            }

            80% {
                transform: translate(-15%, 0)
            }

            90% {
                transform: translate(10%, 5%)
            }
        }

        .orb-1,
        .orb-2 {
            position: fixed;
            width: min(60vw, 600px);
            height: min(60vw, 600px);
            border-radius: 50%;
            filter: blur(min(8vw, 80px));
            pointer-events: none;
            z-index: -1;
            opacity: 0.3;
        }

        .orb-1 {
            top: -20vh;
            left: -20vw;
            background: radial-gradient(circle at 30% 30%, var(--orange-bright), transparent 70%);
            animation: float1 20s infinite ease-in-out;
        }

        .orb-2 {
            bottom: -20vh;
            right: -20vw;
            background: radial-gradient(circle at 70% 70%, #ff5500, transparent 70%);
            animation: float2 25s infinite ease-in-out;
        }

        @keyframes float1 {

            0%,
            100% {
                transform: translate(0, 0) scale(1)
            }

            33% {
                transform: translate(10vw, 10vh) scale(1.1)
            }

            66% {
                transform: translate(-5vw, 15vh) scale(0.9)
            }
        }

        @keyframes float2 {

            0%,
            100% {
                transform: translate(0, 0) scale(1)
            }

            33% {
                transform: translate(-10vw, -10vh) scale(1.2)
            }

            66% {
                transform: translate(5vw, -15vh) scale(0.8)
            }
        }

        .top-bar {
            position: fixed;
            top: 0;
            left: 0;
            right: 0;
            z-index: 1000;
            display: flex;
            justify-content: center;
            align-items: center;
            gap: 0.5rem;
            padding: 0.5rem 1rem;
            pointer-events: none;
        }

        .top-bar-left {
            display: flex;
            gap: 0.5rem;
            position: absolute;
            left: 1rem;
            pointer-events: auto;
        }

        .top-bar-right {
            display: flex;
            gap: 0.5rem;
            position: absolute;
            right: 1rem;
            pointer-events: auto;
        }

        .admin-btn {
            pointer-events: auto;
            padding: 0.5rem 1.1rem;
            border: 1px solid rgba(255, 215, 0, 0.3);
            border-radius: 50px;
            background: rgba(255, 215, 0, 0.12);
            backdrop-filter: blur(10px);
            color: #ffd700;
            font-size: 0.9rem;
            cursor: pointer;
            transition: all 0.3s;
            display: flex;
            align-items: center;
            gap: 0.4rem;
            user-select: none;
            box-shadow: var(--shadow-deep);
            white-space: nowrap;
            text-decoration: none;
        }

        .admin-btn:hover {
            background: rgba(255, 215, 0, 0.25);
            border-color: #ffd700;
            box-shadow: var(--shadow-deep), 0 0 16px rgba(255, 215, 0, 0.35);
            transform: translateY(-1px);
        }

        .upload-btn {
            pointer-events: auto;
            padding: 0.5rem 1.1rem;
            border: 1px solid var(--orange-glow);
            border-radius: 50px;
            background: rgba(255, 140, 0, 0.12);
            backdrop-filter: blur(10px);
            color: var(--orange-bright);
            font-size: 0.9rem;
            cursor: pointer;
            transition: all 0.3s;
            display: flex;
            align-items: center;
            gap: 0.4rem;
            user-select: none;
            box-shadow: var(--shadow-deep);
        }

        .upload-btn:hover {
            background: rgba(255, 140, 0, 0.25);
            border-color: var(--orange-bright);
            box-shadow: var(--shadow-deep), 0 0 16px rgba(255, 140, 0, 0.35);
            transform: translateY(-1px);
        }

        .search-container {
            position: relative;
            flex: 0 1 400px;
            max-width: 400px;
            margin: 0 auto;
            pointer-events: auto;
        }

        .search-input {
            width: 100%;
            padding: 0.5rem 1rem;
            background: rgba(0, 0, 0, 0.6);
            backdrop-filter: blur(10px);
            border: 1px solid var(--orange-glow);
            border-radius: 50px;
            color: var(--text-primary);
            font-size: 0.9rem;
            transition: all 0.3s;
            box-shadow: var(--shadow-deep);
        }

        .search-input:focus {
            outline: none;
            border-color: var(--orange-bright);
            background: rgba(0, 0, 0, 0.8);
            box-shadow: 0 0 0 2px rgba(255, 140, 0, 0.3);
        }

        .search-dropdown {
            position: absolute;
            top: calc(100%+8px);
            left: 0;
            right: 0;
            background: rgba(0, 0, 0, 0.9);
            backdrop-filter: blur(12px);
            border: 1px solid var(--glass-border);
            border-radius: 16px;
            max-height: 400px;
            overflow-y: auto;
            display: none;
            z-index: 2000;
            box-shadow: var(--shadow-deep), var(--shadow-orange);
        }

        .search-dropdown.show {
            display: block;
        }

        .search-result-item {
            display: flex;
            align-items: center;
            gap: 1rem;
            padding: 0.75rem 1rem;
            cursor: pointer;
            transition: background 0.2s;
            border-bottom: 1px solid rgba(255, 255, 255, 0.05);
            user-select: none;
        }

        .search-result-item:last-child {
            border-bottom: none;
        }

        .search-result-item:hover {
            background: rgba(255, 140, 0, 0.15);
        }

        .search-result-image {
            width: 48px;
            height: 48px;
            border-radius: 8px;
            object-fit: cover;
            background: rgba(255, 140, 0, 0.1);
            border: 1px solid var(--glass-border);
            user-select: none;
            pointer-events: none;
        }

        .search-result-info {
            flex: 1;
            user-select: none;
        }

        .search-result-title {
            font-weight: bold;
            color: var(--orange-bright);
            font-size: 0.9rem;
            margin-bottom: 0.2rem;
        }

        .search-result-price {
            font-size: 0.8rem;
            color: var(--text-primary);
            opacity: 0.8;
        }

        .search-result-seller {
            font-size: 0.7rem;
            color: var(--text-primary);
            opacity: 0.6;
        }

        body[data-theme="light"] .search-input {
            background: rgba(245, 252, 215, 0.9);
            border-color: rgba(140, 170, 10, 0.4);
            color: #1a1f00;
            box-shadow: 0 2px 10px rgba(0, 0, 0, 0.05);
        }

        body[data-theme="light"] .search-input:focus {
            background: #fff;
            border-color: #B0CB1F;
            box-shadow: 0 0 0 3px rgba(176, 203, 31, 0.3);
        }

        body[data-theme="light"] .search-dropdown {
            background: rgba(248, 252, 230, 0.98);
            border-color: rgba(140, 170, 10, 0.3);
            box-shadow: 0 10px 30px rgba(0, 0, 0, 0.1), 0 0 20px rgba(176, 203, 31, 0.1);
        }

        body[data-theme="light"] .search-result-item {
            border-bottom-color: rgba(140, 170, 10, 0.15);
        }

        body[data-theme="light"] .search-result-item:hover {
            background: rgba(176, 203, 31, 0.15);
        }

        body[data-theme="light"] .search-result-title {
            color: #7a9200;
        }

        body[data-theme="light"] .search-result-price,
        body[data-theme="light"] .search-result-seller {
            color: #1a1f00;
            opacity: 0.8;
        }

        body[data-theme="light"] .search-result-image {
            background: rgba(176, 203, 31, 0.1);
            border-color: rgba(140, 170, 10, 0.3);
        }

        .account-menu {
            position: relative;
            display: inline-block;
            pointer-events: auto;
        }

        .account-menu-btn {
            display: flex;
            align-items: center;
            gap: 0.3rem;
            padding: 0.5rem 1rem;
            border-radius: 50px;
            border: 1px solid var(--orange-glow);
            background: rgba(0, 0, 0, 0.5);
            backdrop-filter: blur(10px);
            color: var(--orange-bright);
            font-size: 0.9rem;
            font-family: inherit;
            white-space: nowrap;
            cursor: pointer;
            user-select: none;
            box-shadow: 0 4px 15px rgba(0, 0, 0, 0.3);
            transition: background 0.2s, border-color 0.2s;
        }

        .account-menu-btn:hover {
            background: rgba(255, 140, 0, 0.1);
            border-color: var(--orange-bright);
        }

        .account-dropdown {
            position: absolute;
            right: 0;
            top: calc(100%+0.5rem);
            width: 250px;
            z-index: 1001;
            opacity: 0;
            pointer-events: none;
            transform: translateY(-4px);
            transition: opacity 0.18s, transform 0.18s;
        }

        .account-dropdown.show {
            opacity: 1;
            pointer-events: auto;
            transform: translateY(0);
        }

        .account-dropdown-panel {
            background: rgba(0, 0, 0, 0.8);
            backdrop-filter: blur(24px);
            border: 1px solid var(--glass-border);
            border-radius: 16px;
            padding: 0.75rem;
            box-shadow: var(--shadow-deep), var(--shadow-orange);
        }

        .user-info {
            color: var(--text-primary);
            font-size: 0.9rem;
            padding: 0.75rem 1rem;
            user-select: none;
        }

        .user-info strong {
            display: block;
            word-wrap: break-word;
            color: var(--orange-bright);
        }

        .dropdown-divider {
            height: 1px;
            background: linear-gradient(90deg, transparent, var(--orange-bright), transparent);
            margin: 0.5rem 0;
        }

        .account-link {
            display: block;
            width: 100%;
            text-decoration: none;
            color: inherit;
        }

        .account-link span {
            display: block;
            width: 100%;
            font-size: 0.9rem;
            padding: 0.75rem 1rem;
            border-radius: 8px;
            transition: all 0.2s;
            user-select: none;
        }

        .account-link span:hover {
            background: rgba(255, 140, 0, 0.15);
            color: var(--orange-bright);
            transform: translateX(5px);
        }

        .logout-button {
            width: 100%;
            background: transparent;
            border: none;
            padding: 0;
            color: var(--text-primary);
            cursor: pointer;
        }

        .logout-button span {
            display: block;
            width: 100%;
            font-size: 0.9rem;
            padding: 0.75rem 1rem;
            border-radius: 8px;
            transition: all 0.2s;
            user-select: none;
        }

        .theme-toggle-row {
            display: flex;
            align-items: center;
            justify-content: space-between;
            padding: 0.6rem 1rem;
            font-size: 0.85rem;
            color: var(--text-primary);
        }

        .theme-switch {
            position: relative;
            width: 42px;
            height: 24px;
            flex-shrink: 0;
        }

        .theme-switch input {
            opacity: 0;
            width: 0;
            height: 0;
            position: absolute;
        }

        .theme-switch-track {
            position: absolute;
            inset: 0;
            border-radius: 999px;
            background: rgba(255, 255, 255, 0.1);
            border: 1px solid rgba(255, 255, 255, 0.15);
            transition: background 0.3s, border-color 0.3s;
            cursor: pointer;
        }

        .theme-switch input:checked+.theme-switch-track {
            background: rgba(176, 203, 31, 0.25);
            border-color: #B0CB1F;
        }

        .theme-switch-thumb {
            position: absolute;
            top: 3px;
            left: 3px;
            width: 16px;
            height: 16px;
            border-radius: 50%;
            background: rgba(255, 255, 255, 0.5);
            transition: transform 0.3s, background 0.3s;
            pointer-events: none;
        }

        .theme-switch input:checked~.theme-switch-thumb {
            transform: translateX(18px);
            background: #B0CB1F;
        }

        .main-content {
            width: 100%;
            max-width: 100%;
            margin: 0;
            padding: 3rem 0 4rem;
            position: relative;
            z-index: 1;
        }

        .items-grid {
            display: grid;
            gap: 1.2rem;
            width: 100%;
            padding: 1rem;
        }

        @media (orientation:landscape) {
            .items-grid {
                grid-template-columns: repeat(6, 1fr)
            }
        }

        @media (orientation:portrait) {
            .items-grid {
                grid-template-columns: repeat(3, 1fr)
            }
        }

        @media (min-width:1600px) and (orientation:landscape) {
            .items-grid {
                grid-template-columns: repeat(8, 1fr);
                gap: 1.3rem
            }
        }

        @media (max-width:480px) and (orientation:portrait) {
            .items-grid {
                grid-template-columns: repeat(2, 1fr);
                gap: 0.8rem;
                padding: 0.8rem
            }
        }

        @media (max-width:360px) and (orientation:portrait) {
            .items-grid {
                grid-template-columns: repeat(2, 1fr);
                gap: 0.7rem;
                padding: 0.7rem
            }
        }

        @media (min-width:768px) and (max-width:1024px) and (orientation:portrait) {
            .items-grid {
                grid-template-columns: repeat(3, 1fr);
                gap: 1rem
            }
        }

        @media (min-width:768px) and (max-width:1280px) and (orientation:landscape) {
            .items-grid {
                grid-template-columns: repeat(5, 1fr);
                gap: 1rem
            }
        }

        .item-card {
            background: rgba(0, 0, 0, 0.6);
            backdrop-filter: blur(20px);
            border: 1px solid var(--glass-border);
            border-radius: 16px;
            padding: clamp(0.8rem, 1.5vw, 1.2rem);
            transition: all 0.3s cubic-bezier(0.2, 0.9, 0.4, 1.1);
            display: flex;
            flex-direction: column;
            width: 100%;
            height: 100%;
            user-select: none;
            position: relative;
            overflow: hidden;
            cursor: pointer;
            box-shadow: 0 4px 15px rgba(0, 0, 0, 0.3);
        }

        .item-card:hover {
            border-color: var(--orange-bright);
            box-shadow: 0 8px 25px rgba(255, 140, 0, 0.25);
            transform: translateY(-4px);
            background: rgba(0, 0, 0, 0.75);
        }

        .item-image {
            width: 100%;
            aspect-ratio: 1/1;
            object-fit: cover;
            border-radius: 12px;
            margin-bottom: 0.8rem;
            border: 1px solid var(--glass-border);
            flex-shrink: 0;
            transition: transform 0.3s;
        }

        .item-card:hover .item-image {
            transform: scale(1.02);
        }

        .item-image-placeholder {
            width: 100%;
            aspect-ratio: 1/1;
            border-radius: 12px;
            margin-bottom: 0.8rem;
            border: 1px solid var(--glass-border);
            display: flex;
            align-items: center;
            justify-content: center;
            background: var(--placeholder-bg);
            flex-shrink: 0;
        }

        .placeholder-text {
            color: var(--placeholder-text);
            font-size: clamp(0.8rem, 1.5vw, 1.2rem);
        }

        .image-count-badge {
            position: absolute;
            top: 12px;
            left: 12px;
            background: rgba(0, 0, 0, 0.75);
            backdrop-filter: blur(5px);
            padding: 0.3rem 0.7rem;
            border-radius: 20px;
            font-size: clamp(0.6rem, 0.9vw, 0.75rem);
            border: 1px solid var(--orange-glow);
            color: var(--orange-bright);
            font-weight: bold;
            z-index: 2;
        }

        .item-title {
            font-size: clamp(0.75rem, 1.1vw, 1.1rem);
            font-weight: bold;
            color: var(--orange-bright);
            margin-bottom: 0.4rem;
            word-wrap: break-word;
            overflow: hidden;
            display: -webkit-box;
            -webkit-line-clamp: 2;
            -webkit-box-orient: vertical;
            line-height: 1.3;
        }

        .item-price {
            font-size: clamp(0.9rem, 1.3vw, 1.4rem);
            font-weight: bold;
            color: var(--orange-bright);
            margin-bottom: 0.35rem;
            text-shadow: 0 0 10px var(--orange-glow);
        }

        .item-seller {
            font-size: clamp(0.65rem, 0.85vw, 0.85rem);
            color: var(--text-primary);
            opacity: 0.7;
            margin-bottom: 0.3rem;
            overflow: hidden;
            white-space: nowrap;
            text-overflow: ellipsis;
            cursor: pointer;
            transition: color 0.18s;
        }

        .item-seller:hover {
            color: var(--orange-bright);
        }

        .item-date {
            font-size: clamp(0.55rem, 0.7vw, 0.7rem);
            color: var(--text-primary);
            opacity: 0.5;
        }

        .card-menu {
            position: absolute;
            top: 10px;
            right: 10px;
            z-index: 10;
        }

        .card-menu-button {
            color: var(--orange-bright);
            display: flex;
            align-items: center;
            justify-content: center;
            cursor: pointer;
            font-size: 1.5rem;
            transition: all 0.3s;
            background: transparent;
            border: none;
            padding: 0;
            line-height: 1;
        }

        .card-menu-button:hover {
            color: #ffaa33;
            transform: scale(1.1);
        }

        .card-menu-content {
            position: absolute;
            top: 40px;
            right: 0;
            min-width: 150px;
            background: rgba(0, 0, 0, 0.9);
            backdrop-filter: blur(10px);
            border: 1px solid var(--glass-border);
            border-radius: 8px;
            padding: 0.5rem;
            box-shadow: var(--shadow-deep), var(--shadow-orange);
            display: none;
            z-index: 20;
        }

        .card-menu-content.show {
            display: block;
        }

        .card-menu-item {
            width: 100%;
            padding: 0.5rem 1rem;
            background: transparent;
            border: none;
            color: var(--text-primary);
            text-align: left;
            font-size: 0.9rem;
            cursor: pointer;
            border-radius: 4px;
            transition: all 0.2s;
        }

        .card-menu-item:hover {
            background: rgba(255, 140, 0, 0.2);
            color: var(--orange-bright);
        }

        .card-menu-item.delete {
            color: #ff6b6b;
        }

        .card-menu-item.delete:hover {
            background: rgba(255, 0, 0, 0.2);
            color: #ff0000;
        }

        .edit-modal {
            position: fixed;
            inset: 0;
            background: rgba(0, 0, 0, 0.75);
            backdrop-filter: blur(8px);
            display: none;
            align-items: center;
            justify-content: center;
            z-index: 5500;
            opacity: 0;
            transition: opacity 0.3s;
        }

        .edit-modal.show {
            display: flex;
            opacity: 1;
        }

        .edit-modal-content {
            width: 100%;
            max-width: 500px;
            background: var(--glass-bg);
            backdrop-filter: blur(20px);
            border: 1px solid var(--glass-border);
            border-radius: 24px;
            padding: 2rem 1.8rem;
            box-shadow: var(--shadow-deep), var(--shadow-orange);
            transform: translateY(20px) scale(0.98);
            transition: transform 0.3s cubic-bezier(0.34, 1.56, 0.64, 1), opacity 0.3s;
            opacity: 0;
        }

        .edit-modal.show .edit-modal-content {
            transform: translateY(0) scale(1);
            opacity: 1;
        }

        .edit-modal-header {
            display: flex;
            justify-content: space-between;
            align-items: center;
            margin-bottom: 1.5rem;
        }

        .edit-modal-title {
            font-size: 1.4rem;
            font-weight: 700;
            color: var(--orange-bright);
        }

        .edit-modal-close {
            background: rgba(0, 0, 0, 0.5);
            border: 1px solid var(--glass-border);
            border-radius: 50%;
            color: var(--orange-bright);
            font-size: 1.3rem;
            cursor: pointer;
            width: 36px;
            height: 36px;
            display: flex;
            align-items: center;
            justify-content: center;
        }

        .edit-modal-close:hover {
            background: rgba(255, 140, 0, 0.2);
            border-color: var(--orange-bright);
        }

        .edit-form-group {
            margin-bottom: 1.2rem;
        }

        .edit-form-label {
            display: block;
            font-size: 0.75rem;
            font-weight: 600;
            text-transform: uppercase;
            color: var(--orange-bright);
            margin-bottom: 0.4rem;
        }

        .edit-form-input,
        .edit-form-textarea {
            width: 100%;
            background: var(--input-bg);
            border: 1px solid var(--glass-border);
            border-radius: 12px;
            padding: 0.75rem 1rem;
            color: var(--text-primary);
            font-family: inherit;
            font-size: 0.9rem;
            transition: all 0.25s;
            outline: none;
        }

        .edit-form-textarea {
            resize: none;
            height: 120px;
            overflow-y: auto;
        }

        .edit-form-input:focus,
        .edit-form-textarea:focus {
            border-color: var(--orange-bright);
            box-shadow: 0 0 0 3px rgba(255, 140, 0, 0.15);
        }

        .edit-price-wrapper {
            position: relative;
            display: flex;
            align-items: center;
        }

        .edit-price-wrapper .edit-form-input {
            padding-right: 3rem;
        }

        .edit-price-suffix {
            position: absolute;
            right: 1rem;
            color: var(--orange-bright);
            font-weight: 600;
            font-size: 0.9rem;
            pointer-events: none;
            user-select: none;
        }

        .report-modal {
            position: fixed;
            inset: 0;
            background: rgba(0, 0, 0, 0.8);
            backdrop-filter: blur(8px);
            display: none;
            align-items: center;
            justify-content: center;
            z-index: 4500;
        }

        .report-modal.show {
            display: flex;
        }

        .report-modal-content {
            background: rgba(10, 10, 10, 0.95);
            border: 1px solid var(--orange-bright);
            border-radius: 16px;
            padding: 2rem;
            max-width: 500px;
            width: 90%;
            max-height: 90vh;
            overflow-y: auto;
        }

        .report-modal-header {
            display: flex;
            justify-content: space-between;
            align-items: center;
            margin-bottom: 1.5rem;
        }

        .report-modal-title {
            font-size: 1.3rem;
            color: var(--orange-bright);
        }

        .report-modal-close {
            background: transparent;
            border: none;
            color: rgba(255, 255, 255, 0.5);
            font-size: 1.5rem;
            cursor: pointer;
        }

        .report-modal-close:hover {
            color: var(--orange-bright);
        }

        .report-submit-btn {
            width: 100%;
            padding: 0.75rem;
            background: linear-gradient(135deg, #1aff6e, #00c851) !important;
            border: none !important;
            border-radius: 12px !important;
            color: #001a08 !important;
            font-size: 1rem;
            font-weight: 800;
            cursor: pointer;
            box-shadow: 0 4px 22px rgba(57, 255, 110, 0.35) !important;
        }

        .report-submit-btn:hover {
            background: linear-gradient(135deg, #39ff6e, #00e85c) !important;
            box-shadow: 0 6px 30px rgba(57, 255, 110, 0.55) !important;
            transform: translateY(-2px);
        }

        .floating-pagination {
            position: fixed;
            bottom: 20px;
            left: 0;
            right: 0;
            display: flex;
            justify-content: center;
            z-index: 1000;
            pointer-events: none;
        }

        .pagination-container {
            display: flex;
            gap: 1rem;
            background: rgba(0, 0, 0, 0.7);
            backdrop-filter: blur(10px);
            padding: 0.75rem 1.5rem;
            border-radius: 50px;
            border: 1px solid var(--glass-border);
            box-shadow: var(--shadow-deep), var(--shadow-orange);
            pointer-events: auto;
        }

        .pagination-btn {
            padding: 0.5rem 1.5rem;
            background: rgba(255, 140, 0, 0.1);
            border: 1px solid var(--orange-glow);
            border-radius: 50px;
            color: var(--text-primary);
            text-decoration: none;
            font-size: 1rem;
            transition: all 0.3s;
            cursor: pointer;
        }

        .pagination-btn:hover {
            background: rgba(255, 140, 0, 0.2);
            color: var(--orange-bright);
            transform: translateY(-2px);
        }

        .pagination-btn.disabled {
            background: rgba(255, 255, 255, 0.05);
            border-color: var(--glass-border);
            opacity: 0.5;
            cursor: not-allowed;
            pointer-events: none;
        }

        .unselectable {
            user-select: none;
            -webkit-user-select: none;
        }

        .modal-overlay {
            position: fixed;
            inset: 0;
            z-index: 2000;
            background: rgba(0, 0, 0, 0.75);
            backdrop-filter: blur(6px);
            display: flex;
            align-items: center;
            justify-content: center;
            padding: 1rem;
            opacity: 0;
            pointer-events: none;
            transition: opacity 0.3s;
        }

        .modal-overlay.active {
            opacity: 1;
            pointer-events: auto;
        }

        .modal-card {
            width: 100%;
            max-width: 620px;
            max-height: 90vh;
            overflow-y: auto;
            background: rgba(10, 10, 10, 0.92);
            border: 1px solid rgba(255, 140, 0, 0.35);
            border-radius: 24px;
            padding: 2.5rem 2rem;
            box-shadow: 0 20px 60px rgba(0, 0, 0, 0.7), 0 0 40px rgba(255, 140, 0, 0.15);
            position: relative;
            transform: translateY(30px) scale(0.97);
            transition: transform 0.35s cubic-bezier(0.34, 1.56, 0.64, 1), opacity 0.3s;
            opacity: 0;
        }

        .modal-overlay.active .modal-card {
            transform: translateY(0) scale(1);
            opacity: 1;
        }

        .modal-close {
            position: absolute;
            top: 1.1rem;
            right: 1.2rem;
            background: transparent;
            border: none;
            color: rgba(255, 255, 255, 0.4);
            font-size: 1.4rem;
            cursor: pointer;
        }

        .modal-close:hover {
            color: var(--orange-bright);
        }

        .modal-title {
            font-size: 1.5rem;
            font-weight: 700;
            color: var(--orange-bright);
            margin-bottom: 0.3rem;
        }

        .modal-subtitle {
            font-size: 0.82rem;
            color: rgba(255, 255, 255, 0.4);
            margin-bottom: 1.8rem;
        }

        .form-group {
            margin-bottom: 1.3rem;
        }

        .form-label {
            display: block;
            font-size: 0.8rem;
            font-weight: 600;
            color: rgba(255, 255, 255, 0.6);
            text-transform: uppercase;
            letter-spacing: 0.08em;
            margin-bottom: 0.45rem;
        }

        .form-label .required-star {
            color: var(--orange-bright);
        }

        .form-input,
        .form-textarea {
            width: 100%;
            background: rgba(255, 255, 255, 0.04);
            border: 1px solid rgba(255, 140, 0, 0.2);
            border-radius: 12px;
            padding: 0.75rem 1rem;
            color: var(--text-primary);
            font-size: 0.95rem;
            font-family: inherit;
            transition: border-color 0.25s, box-shadow 0.25s;
            outline: none;
        }

        .form-input:focus,
        .form-textarea:focus {
            border-color: var(--orange-bright);
            background: rgba(255, 140, 0, 0.06);
            box-shadow: 0 0 0 3px rgba(255, 140, 0, 0.12);
        }

        .form-input::placeholder,
        .form-textarea::placeholder {
            color: rgba(255, 255, 255, 0.2);
        }

        .image-upload-container {
            background: rgba(0, 0, 0, 0.3);
            border: 2px dashed rgba(255, 140, 0, 0.3);
            border-radius: 16px;
            padding: 1.5rem;
            margin-bottom: 1.5rem;
            transition: all 0.3s;
        }

        .image-upload-container:hover {
            border-color: var(--orange-bright);
            background: rgba(255, 140, 0, 0.05);
        }

        .image-upload-label {
            display: flex;
            flex-direction: column;
            align-items: center;
            gap: 0.5rem;
            cursor: pointer;
            color: rgba(255, 255, 255, 0.6);
        }

        .image-upload-icon {
            font-size: 2rem;
            color: var(--orange-bright);
        }

        #item_images {
            display: none;
        }

        .image-preview-container {
            display: grid;
            grid-template-columns: repeat(auto-fill, minmax(100px, 1fr));
            gap: 1rem;
            margin-top: 1rem;
        }

        .image-preview-item {
            position: relative;
            aspect-ratio: 1;
            border-radius: 8px;
            overflow: hidden;
            border: 2px solid rgba(255, 140, 0, 0.3);
        }

        .image-preview-item img {
            width: 100%;
            height: 100%;
            object-fit: cover;
        }

        .image-preview-remove {
            position: absolute;
            top: 5px;
            right: 5px;
            width: 24px;
            height: 24px;
            background: rgba(0, 0, 0, 0.7);
            border: 1px solid var(--orange-bright);
            border-radius: 50%;
            color: #fff;
            display: flex;
            align-items: center;
            justify-content: center;
            cursor: pointer;
            font-size: 16px;
        }

        .image-preview-remove:hover {
            background: rgba(255, 0, 0, 0.7);
            border-color: red;
        }

        .primary-badge {
            position: absolute;
            bottom: 5px;
            left: 5px;
            background: var(--orange-bright);
            color: #000;
            padding: 0.2rem 0.4rem;
            border-radius: 4px;
            font-size: 0.6rem;
            font-weight: bold;
        }

        .price-wrapper {
            position: relative;
            display: flex;
            align-items: center;
        }

        .price-wrapper .form-input {
            padding-right: 3rem;
        }

        .price-suffix {
            position: absolute;
            right: 1rem;
            color: rgba(255, 140, 0, 0.6);
            font-weight: 600;
            font-size: 0.95rem;
            pointer-events: none;
        }

        .field-error {
            display: none;
            font-size: 0.76rem;
            color: #ff4d4d;
            margin-top: 0.35rem;
        }

        .form-input.invalid,
        .form-textarea.invalid {
            border-color: #ff4d4d;
            box-shadow: 0 0 0 3px rgba(255, 77, 77, 0.12);
        }

        .error-banner {
            background: rgba(255, 60, 60, 0.1);
            border: 1px solid rgba(255, 60, 60, 0.3);
            border-radius: 10px;
            padding: 0.75rem 1rem;
            color: #ff8080;
            font-size: 0.87rem;
            margin-bottom: 1.3rem;
        }

        .success-banner {
            background: rgba(0, 200, 100, 0.1);
            border: 1px solid rgba(0, 200, 100, 0.3);
            border-radius: 10px;
            padding: 0.75rem 1rem;
            color: #5dffa0;
            font-size: 0.87rem;
            margin-bottom: 1.3rem;
            display: flex;
            align-items: center;
            gap: 0.5rem;
        }

        .submit-btn {
            width: 100%;
            padding: 0.85rem;
            background: linear-gradient(135deg, rgba(255, 140, 0, 0.9), rgba(255, 85, 0, 0.9));
            border: none;
            border-radius: 12px;
            color: #fff;
            font-size: 1rem;
            font-weight: 700;
            cursor: pointer;
            letter-spacing: 0.03em;
            transition: all 0.25s;
            margin-top: 0.5rem;
            box-shadow: 0 4px 20px rgba(255, 140, 0, 0.3);
        }

        .submit-btn:hover {
            background: linear-gradient(135deg, #ff8c00, #ff4400);
            box-shadow: 0 6px 28px rgba(255, 140, 0, 0.5);
            transform: translateY(-2px);
        }

        /* DELETE CONFIRM MODAL */
        .delete-confirm-modal {
            position: fixed;
            inset: 0;
            background: rgba(0, 0, 0, 0.8);
            backdrop-filter: blur(8px);
            display: none;
            align-items: center;
            justify-content: center;
            z-index: 5000;
        }

        .delete-confirm-modal.show {
            display: flex;
        }

        .delete-confirm-modal-content {
            background: rgba(20, 10, 10, 0.95);
            border: 2px solid #ff4444;
            border-radius: 20px;
            padding: 2rem;
            max-width: 450px;
            width: 90%;
            box-shadow: 0 20px 60px rgba(255, 0, 0, 0.3);
        }

        .delete-confirm-modal-header {
            display: flex;
            align-items: center;
            gap: 0.8rem;
            margin-bottom: 1.5rem;
        }

        .delete-confirm-modal-icon {
            font-size: 2rem;
        }

        .delete-confirm-modal-title {
            font-size: 1.3rem;
            color: #ff4444;
            flex: 1;
        }

        .delete-confirm-modal-close {
            background: transparent;
            border: none;
            color: rgba(255, 255, 255, 0.5);
            font-size: 1.5rem;
            cursor: pointer;
        }

        .delete-confirm-modal-close:hover {
            color: #ff4444;
        }

        .delete-confirm-modal-body {
            margin-bottom: 1.5rem;
        }

        .delete-confirm-modal-text {
            font-size: 1rem;
            color: #fff;
            margin-bottom: 0.5rem;
        }

        .delete-confirm-modal-warning {
            font-size: 0.85rem;
            color: rgba(255, 255, 255, 0.5);
        }

        .delete-confirm-modal-actions {
            display: flex;
            gap: 1rem;
            justify-content: flex-end;
        }

        .delete-confirm-cancel-btn {
            padding: 0.7rem 1.5rem;
            border-radius: 12px;
            border: 1px solid rgba(255, 255, 255, 0.3);
            background: transparent;
            color: #fff;
            font-size: 0.9rem;
            cursor: pointer;
        }

        .delete-confirm-cancel-btn:hover {
            background: rgba(255, 255, 255, 0.1);
        }

        .delete-confirm-delete-btn {
            padding: 0.7rem 1.5rem;
            border-radius: 12px;
            border: none;
            background: #ff4444;
            color: #fff;
            font-size: 0.9rem;
            font-weight: bold;
            cursor: pointer;
        }

        .delete-confirm-delete-btn:hover {
            background: #ff6666;
            box-shadow: 0 4px 15px rgba(255, 0, 0, 0.4);
        }

        body[data-theme="light"] .delete-confirm-modal {
            background: rgba(220, 230, 180, 0.85) !important;
        }

        body[data-theme="light"] .delete-confirm-modal-content {
            background: rgba(255, 245, 240, 0.98) !important;
            border-color: #d32f2f !important;
            box-shadow: 0 20px 60px rgba(200, 0, 0, 0.2) !important;
        }

        body[data-theme="light"] .delete-confirm-modal-title {
            color: #d32f2f !important;
        }

        body[data-theme="light"] .delete-confirm-modal-text {
            color: #1a1f00 !important;
        }

        body[data-theme="light"] .delete-confirm-modal-warning {
            color: rgba(26, 31, 0, 0.5) !important;
        }

        body[data-theme="light"] .delete-confirm-modal-close {
            color: rgba(26, 31, 0, 0.5) !important;
        }

        body[data-theme="light"] .delete-confirm-modal-close:hover {
            color: #d32f2f !important;
        }

        body[data-theme="light"] .delete-confirm-cancel-btn {
            border-color: rgba(26, 31, 0, 0.3) !important;
            color: #1a1f00 !important;
        }

        body[data-theme="light"] .delete-confirm-cancel-btn:hover {
            background: rgba(0, 0, 0, 0.05) !important;
        }

        body[data-theme="light"] .delete-confirm-delete-btn {
            background: #d32f2f !important;
        }

        body[data-theme="light"] .delete-confirm-delete-btn:hover {
            background: #ff4444 !important;
            box-shadow: 0 4px 15px rgba(200, 0, 0, 0.3) !important;
        }

        .product-modal-overlay {
            position: fixed;
            inset: 0;
            z-index: 4000;
            background: rgba(0, 0, 0, 0.98);
            backdrop-filter: blur(10px);
            display: none;
            align-items: center;
            justify-content: center;
            opacity: 0;
            transition: opacity 0.3s;
        }

        .product-modal-overlay.active {
            display: flex;
            opacity: 1;
        }

        .product-modal-card {
            width: 100vw;
            height: 100vh;
            background: rgba(5, 5, 5, 0.99);
            display: grid;
            grid-template-columns: 1.5fr 1fr;
            gap: 2rem;
            padding: 2rem;
            transform: scale(0.98);
            transition: transform 0.3s;
            overflow: hidden;
        }

        .product-modal-overlay.active .product-modal-card {
            transform: scale(1);
        }

        .product-modal-header {
            position: absolute;
            top: 1.5rem;
            right: 1.5rem;
            display: flex;
            gap: 1rem;
            z-index: 100;
        }

        .product-modal-close {
            width: 48px;
            height: 48px;
            background: rgba(20, 20, 20, 0.8);
            border: 1px solid var(--orange-bright);
            color: var(--orange-bright);
            font-size: 1.8rem;
            cursor: pointer;
            display: flex;
            align-items: center;
            justify-content: center;
            border-radius: 50%;
            transition: all 0.2s;
        }

        .product-modal-close:hover {
            background: var(--orange-bright);
            color: #000;
            transform: scale(1.1);
        }

        .product-menu {
            position: relative;
        }

        .product-menu-button {
            width: 48px;
            height: 48px;
            background: rgba(20, 20, 20, 0.8);
            border: 1px solid var(--orange-bright);
            border-radius: 50%;
            color: var(--orange-bright);
            font-size: 2rem;
            cursor: pointer;
            display: flex;
            align-items: center;
            justify-content: center;
            transition: all 0.2s;
        }

        .product-menu-button:hover {
            background: var(--orange-bright);
            color: #000;
            transform: scale(1.1);
        }

        .product-menu-content {
            position: absolute;
            top: 55px;
            right: 0;
            min-width: 180px;
            background: rgba(10, 10, 10, 0.95);
            backdrop-filter: blur(10px);
            border: 1px solid var(--orange-bright);
            border-radius: 12px;
            padding: 0.5rem;
            box-shadow: 0 10px 30px rgba(0, 0, 0, 0.5), 0 0 30px rgba(255, 140, 0, 0.2);
            display: none;
            z-index: 101;
        }

        .product-menu-content.show {
            display: block;
        }

        .product-menu-item {
            width: 100%;
            padding: 0.75rem 1rem;
            background: transparent;
            border: none;
            color: #fff;
            text-align: left;
            font-size: 1rem;
            cursor: pointer;
            border-radius: 6px;
            transition: all 0.2s;
        }

        .product-menu-item:hover {
            background: rgba(255, 140, 0, 0.2);
            color: var(--orange-bright);
        }

        .product-menu-item.delete:hover {
            background: rgba(255, 0, 0, 0.2);
            color: #ff0000;
        }

        .product-gallery {
            position: relative;
            height: 100%;
            display: flex;
            flex-direction: column;
            background: rgba(0, 0, 0, 0.3);
            border-radius: 24px;
            padding: 1rem;
        }

        .product-main-image-container {
            position: relative;
            width: 100%;
            border-radius: 20px;
            overflow: hidden;
            border: 1px solid var(--glass-border);
            margin-bottom: 1rem;
            background: rgba(0, 0, 0, 0.5);
            display: flex;
            align-items: center;
            justify-content: center;
            min-height: 300px;
        }

        .product-main-image {
            max-width: 100%;
            max-height: 100%;
            object-fit: contain;
            cursor: pointer;
        }

        .product-main-image:hover {
            opacity: 0.9;
        }

        .gallery-nav {
            position: absolute;
            top: 50%;
            transform: translateY(-50%);
            background: rgba(0, 0, 0, 0.7);
            color: #fff;
            border: 2px solid var(--orange-bright);
            width: 50px;
            height: 50px;
            border-radius: 50%;
            cursor: pointer;
            display: flex;
            align-items: center;
            justify-content: center;
            font-size: 1.5rem;
            transition: all 0.2s;
            z-index: 10;
        }

        .gallery-nav:hover {
            background: var(--orange-bright);
            color: #000;
            transform: translateY(-50%) scale(1.1);
        }

        .gallery-nav.prev {
            left: 20px;
        }

        .gallery-nav.next {
            right: 20px;
        }

        .gallery-nav.hidden {
            display: none;
        }

        .product-thumbnails {
            display: flex;
            gap: 1rem;
            overflow-x: auto;
            padding: 0.5rem 0;
            min-height: 100px;
        }

        .product-thumbnail {
            width: 100px;
            height: 100px;
            border-radius: 12px;
            overflow: hidden;
            cursor: pointer;
            border: 3px solid transparent;
            transition: all 0.2s;
            flex-shrink: 0;
        }

        .product-thumbnail:hover {
            border-color: var(--orange-bright);
            transform: translateY(-2px);
        }

        .product-thumbnail.active {
            border-color: var(--orange-bright);
            box-shadow: 0 0 20px var(--orange-glow);
        }

        .product-thumbnail img {
            width: 100%;
            height: 100%;
            object-fit: cover;
        }

        .product-details {
            display: flex;
            flex-direction: column;
            gap: 2rem;
            padding: 2rem;
            background: rgba(10, 10, 10, 0.8);
            border-radius: 24px;
            border: 1px solid var(--glass-border);
            height: 100%;
            overflow-y: auto;
        }

        .product-title {
            font-size: 2.5rem;
            color: var(--orange-bright);
            word-break: break-word;
            line-height: 1.2;
            font-weight: bold;
        }

        .product-price {
            font-size: 3rem;
            font-weight: bold;
            color: var(--orange-bright);
            text-shadow: 0 0 30px var(--orange-glow);
        }

        .product-seller {
            font-size: 1.2rem;
            color: rgba(255, 255, 255, 0.7);
            cursor: pointer;
        }

        .product-seller strong {
            color: var(--orange-bright);
            font-size: 1.4rem;
        }

        .product-date {
            font-size: 1rem;
            color: rgba(255, 255, 255, 0.4);
        }

        .product-description {
            font-size: 1.1rem;
            line-height: 1.8;
            color: rgba(255, 255, 255, 0.9);
            background: rgba(0, 0, 0, 0.5);
            border-radius: 16px;
            padding: 2rem;
            border: 1px solid var(--glass-border);
            max-height: 400px;
            overflow-y: auto;
            white-space: pre-wrap;
        }

        .product-buy-btn {
            background: linear-gradient(135deg, #00c851, #007e33);
            border: none;
            border-radius: 16px;
            padding: 1.5rem 2rem;
            color: #fff;
            font-size: 1.5rem;
            font-weight: bold;
            cursor: pointer;
            margin-top: auto;
            display: flex;
            align-items: center;
            justify-content: center;
            gap: 1rem;
        }

        .product-buy-btn:hover {
            transform: translateY(-2px);
            box-shadow: 0 10px 30px rgba(0, 200, 0, 0.4);
        }

        .lightbox-overlay {
            position: fixed;
            inset: 0;
            z-index: 5000;
            background: rgba(0, 0, 0, 0.95);
            backdrop-filter: blur(10px);
            display: none;
            align-items: center;
            justify-content: center;
            opacity: 0;
            transition: opacity 0.3s;
        }

        .lightbox-overlay.active {
            display: flex;
            opacity: 1;
        }

        .lightbox-content {
            display: flex;
            align-items: flex-start;
            gap: 1rem;
            max-width: 95vw;
            max-height: 95vh;
        }

        .lightbox-image {
            max-width: calc(95vw-70px);
            max-height: 95vh;
            object-fit: contain;
            border: 2px solid var(--orange-bright);
            border-radius: 8px;
        }

        .lightbox-close {
            width: 48px;
            height: 48px;
            background: rgba(20, 20, 20, 0.9);
            border: 1px solid var(--orange-bright);
            color: var(--orange-bright);
            font-size: 2rem;
            cursor: pointer;
            display: flex;
            align-items: center;
            justify-content: center;
            border-radius: 50%;
            flex-shrink: 0;
        }

        .lightbox-close:hover {
            background: var(--orange-bright);
            color: #000;
            transform: scale(1.1);
        }

        .floating-messages-btn {
            position: fixed;
            bottom: 2rem;
            right: 2rem;
            z-index: 3000;
            width: 58px;
            height: 58px;
            border-radius: 50%;
            background: linear-gradient(135deg, #007bff, #0056b3) !important;
            border: none;
            color: #fff !important;
            font-size: 1.5rem;
            cursor: pointer;
            display: flex;
            align-items: center;
            justify-content: center;
            box-shadow: 0 6px 24px rgba(0, 123, 255, 0.5) !important;
            transition: all 0.25s;
            text-decoration: none;
        }

        .floating-messages-btn:hover {
            transform: scale(1.1) translateY(-2px);
            box-shadow: 0 10px 32px rgba(0, 123, 255, 0.7) !important;
            background: linear-gradient(135deg, #0069d9, #004085) !important;
        }

        .floating-messages-badge {
            position: absolute;
            top: -4px;
            right: -4px;
            background: #ff2222;
            color: #fff;
            border-radius: 50%;
            width: 20px;
            height: 20px;
            font-size: 0.7rem;
            font-weight: 700;
            display: flex;
            align-items: center;
            justify-content: center;
            border: 2px solid #0a0a0a;
        }

        /* SELLER POPUP – javított light mode + görgethető összes hirdetés */
        .seller-popup-overlay {
            position: fixed;
            inset: 0;
            z-index: 6000;
            background: rgba(0, 0, 0, 0.98);
            backdrop-filter: blur(16px);
            display: none;
            align-items: center;
            justify-content: center;
            opacity: 0;
            transition: opacity 0.3s;
        }

        .seller-popup-overlay.active {
            display: flex;
            opacity: 1;
        }

        .seller-popup-card {
            width: 100vw;
            height: 100vh;
            background: rgba(5, 5, 5, 0.99);
            overflow-y: auto;
            position: relative;
            transform: scale(0.98);
            transition: transform 0.3s;
            display: flex;
            flex-direction: column;
        }

        .seller-popup-overlay.active .seller-popup-card {
            transform: scale(1);
        }

        .seller-popup-topbar {
            position: sticky;
            top: 0;
            z-index: 10;
            display: flex;
            align-items: center;
            gap: 1rem;
            padding: 0.75rem 1.5rem;
            background: rgba(5, 5, 5, 0.92);
            backdrop-filter: blur(12px);
            border-bottom: 1px solid var(--glass-border);
        }

        .seller-popup-close {
            background: rgba(255, 140, 0, 0.1);
            border: 1px solid var(--glass-border);
            color: var(--orange-bright);
            width: 42px;
            height: 42px;
            border-radius: 50%;
            cursor: pointer;
            font-size: 1.2rem;
            display: flex;
            align-items: center;
            justify-content: center;
            flex-shrink: 0;
        }

        .seller-popup-close:hover {
            background: var(--orange-bright);
            color: #000;
        }

        .seller-popup-topbar-title {
            font-size: 1rem;
            font-weight: 700;
            color: var(--orange-bright);
            flex: 1;
        }

        .seller-popup-body {
            flex: 1;
            max-width: 560px;
            width: 100%;
            margin: 0 auto;
            padding: 2.5rem 1.5rem 3rem;
            display: flex;
            flex-direction: column;
        }

        .seller-popup-avatar {
            width: 90px;
            height: 90px;
            border-radius: 50%;
            background: linear-gradient(135deg, var(--orange-bright), #ff5500);
            display: flex;
            align-items: center;
            justify-content: center;
            font-size: 2.4rem;
            font-weight: 700;
            color: #000;
            margin: 0 auto 1.2rem;
            box-shadow: 0 0 40px rgba(255, 140, 0, 0.3);
            overflow: hidden;
        }

        .seller-popup-avatar-img {
            width: 100%;
            height: 100%;
            object-fit: cover;
        }

        .seller-popup-name {
            text-align: center;
            font-size: 1.8rem;
            font-weight: 700;
            color: var(--orange-bright);
            margin-bottom: 0.35rem;
        }

        .seller-popup-meta {
            text-align: center;
            font-size: 0.88rem;
            color: rgba(255, 255, 255, 0.4);
            margin-bottom: 2rem;
        }

        .seller-popup-stats {
            display: flex;
            gap: 1rem;
            margin-bottom: 2rem;
        }

        .seller-stat {
            flex: 1;
            background: rgba(255, 140, 0, 0.07);
            border: 1px solid rgba(255, 140, 0, 0.15);
            border-radius: 16px;
            padding: 1.1rem;
            text-align: center;
        }

        .seller-stat-value {
            font-size: 1.6rem;
            font-weight: 700;
            color: var(--orange-bright);
        }

        .seller-stat-label {
            font-size: 0.78rem;
            color: rgba(255, 255, 255, 0.4);
            margin-top: 3px;
        }

        .seller-popup-items-title {
            font-size: 0.8rem;
            text-transform: uppercase;
            letter-spacing: 0.1em;
            color: rgba(255, 255, 255, 0.3);
            margin-bottom: 0.9rem;
        }

        .seller-popup-items-grid {
            display: grid;
            grid-template-columns: repeat(2, 1fr);
            gap: 0.8rem;
            margin-bottom: 2rem;
            max-height: 60vh;
            overflow-y: auto;
            padding-right: 4px;
        }

        .seller-item-thumb {
            border-radius: 14px;
            overflow: hidden;
            border: 1px solid rgba(255, 140, 0, 0.12);
            cursor: pointer;
            transition: all 0.2s;
            background: rgba(0, 0, 0, 0.4);
        }

        .seller-item-thumb:hover {
            border-color: var(--orange-bright);
            transform: translateY(-3px);
            box-shadow: 0 8px 24px rgba(255, 140, 0, 0.2);
        }

        .seller-item-thumb img {
            width: 100%;
            height: 110px;
            object-fit: cover;
            display: block;
        }

        .seller-item-thumb-placeholder {
            width: 100%;
            height: 110px;
            background: rgba(255, 140, 0, 0.07);
            display: flex;
            align-items: center;
            justify-content: center;
            color: rgba(255, 140, 0, 0.35);
            font-size: 1.8rem;
        }

        .seller-item-info {
            padding: 0.6rem 0.75rem;
        }

        .seller-item-title {
            font-size: 0.82rem;
            white-space: nowrap;
            overflow: hidden;
            text-overflow: ellipsis;
            color: rgba(255, 255, 255, 0.85);
        }

        .seller-item-price {
            font-size: 0.8rem;
            color: var(--orange-bright);
            font-weight: 600;
            margin-top: 3px;
        }

        .seller-popup-msg-btn {
            width: 100%;
            padding: 1.1rem;
            background: linear-gradient(135deg, var(--orange-bright), #ff5500);
            border: none;
            border-radius: 16px;
            color: #fff;
            font-size: 1.05rem;
            font-weight: 700;
            cursor: pointer;
            display: flex;
            align-items: center;
            justify-content: center;
            gap: 0.6rem;
            text-decoration: none;
            margin-top: auto;
        }

        .seller-popup-msg-btn:hover {
            transform: translateY(-2px);
            box-shadow: 0 10px 30px rgba(255, 140, 0, 0.4);
        }

        /* LIGHT MODE SELLER POPUP FIXES */
        body[data-theme="light"] .seller-popup-avatar {
            box-shadow: 0 0 40px rgba(176, 203, 31, 0.3) !important;
            background: linear-gradient(135deg, #B0CB1F, #8aA000) !important;
        }

        body[data-theme="light"] .seller-stat-label {
            color: rgba(26, 31, 0, 0.6) !important;
        }

        body[data-theme="light"] .seller-popup-meta {
            color: rgba(26, 31, 0, 0.5) !important;
        }

        body[data-theme="light"] .seller-item-title {
            color: rgba(26, 31, 0, 0.85) !important;
        }

        body[data-theme="light"] .seller-popup-items-title {
            color: rgba(26, 31, 0, 0.5) !important;
        }

        .admin-badge {
            font-size: 0.7rem;
            background: rgba(255, 215, 0, 0.2);
            color: #ffd700;
            border: 1px solid rgba(255, 215, 0, 0.4);
            border-radius: 50px;
            padding: 1px 8px;
        }

        @media (max-width:600px) {
            .top-bar {
                flex-wrap: wrap;
                justify-content: space-between;
                padding: 0.5rem;
            }

            .top-bar-left,
            .top-bar-right {
                position: static;
            }

            .search-container {
                order: 3;
                width: 100%;
                max-width: none;
            }

            .upload-btn .button-text,
            .admin-btn .button-text {
                display: none;
            }

            .account-dropdown {
                width: 240px;
            }
        }
    </style>
</head>

<body>
    <div class="noise"></div>
    <div class="orb-1"></div>
    <div class="orb-2"></div>

    <div class="top-bar">
        <div class="top-bar-left">
            <?php if ($isAdmin): ?>
                <a href="admin.php" class="admin-btn unselectable"><span class="shield-icon">🛡️</span><span class="button-text">Admin</span></a>
            <?php endif; ?>
        </div>
        <div class="search-container">
            <input type="text" id="searchInput" class="search-input" placeholder="Keresés termékek között..." autocomplete="off">
            <div id="searchResults" class="search-dropdown"></div>
        </div>
        <div class="top-bar-right">
            <button class="upload-btn unselectable" id="openModalBtn" type="button"><span class="plus-icon">＋</span><span class="button-text">Hirdetés feladása</span></button>
            <div class="account-menu">
                <button type="button" class="account-menu-btn unselectable" id="accountMenuBtn"><span>⚙️</span><span class="button-text">FIÓK</span></button>
                <div class="account-dropdown" id="accountDropdown">
                    <div class="account-dropdown-panel">
                        <div class="user-info"><strong><?php echo htmlspecialchars($_SESSION['username']); ?></strong></div>
                        <div class="dropdown-divider"></div>
                        <a href="account.php" class="account-link"><span>👤 Fiókom</span></a>
                        <div class="dropdown-divider"></div>
                        <div class="theme-toggle-row">
                            <span class="theme-toggle-label">☀️ Világos mód</span>
                            <label class="theme-switch"><input type="checkbox" id="themeSwitchMain"><span class="theme-switch-track"></span><span class="theme-switch-thumb"></span></label>
                        </div>
                        <div class="dropdown-divider"></div>
                        <form method="post" style="width:100%;margin:0">
                            <button type="submit" name="logout" class="logout-button"><span>Kijelentkezés</span></button>
                        </form>
                    </div>
                </div>
            </div>
        </div>
    </div>

    <!-- Upload Modal -->
    <div class="modal-overlay" id="uploadModal">
        <div class="modal-card">
            <button class="modal-close" id="closeModalBtn">✕</button>
            <div class="modal-title">Új hirdetés</div>
            <div class="modal-subtitle">Tölts fel legalább 1 képet</div>
            <?php if (isset($_GET['upload']) && $_GET['upload'] === 'success'): ?><div class="success-banner">✓ Sikeresen feladva!</div><?php endif; ?>
            <?php if ($uploadError): ?><div class="error-banner"><?= htmlspecialchars($uploadError) ?></div><?php endif; ?>
            <form method="post" id="uploadForm" enctype="multipart/form-data">
                <div class="image-upload-container">
                    <label for="item_images" class="image-upload-label"><span class="image-upload-icon">📸</span><span>Kattints a képek kiválasztásához</span></label>
                    <input type="file" id="item_images" name="item_images[]" accept="image/jpeg,image/png,image/gif,image/webp" multiple>
                    <div class="image-preview-container" id="imagePreview"></div>
                    <div class="field-error" id="images-error">Legalább egy kép kell!</div>
                </div>
                <div class="form-group">
                    <label class="form-label">Cím <span class="required-star">*</span></label>
                    <input class="form-input" id="item_title" name="item_title" placeholder="pl. iPhone 14" value="<?= htmlspecialchars($formData['item_title'] ?? '') ?>">
                    <div class="field-error" id="title-error">Kötelező!</div>
                </div>
                <div class="form-group">
                    <label class="form-label">Leírás <span class="required-star">*</span></label>
                    <textarea class="form-textarea" id="item_description" name="item_description"><?= htmlspecialchars($formData['item_description'] ?? '') ?></textarea>
                    <div class="field-error" id="desc-error">Kötelező!</div>
                </div>
                <div class="form-group">
                    <label class="form-label">Ár <span class="required-star">*</span></label>
                    <div class="price-wrapper">
                        <input class="form-input" id="item_price" name="item_price" placeholder="0" value="<?= htmlspecialchars($formData['item_price'] ?? '') ?>">
                        <span class="price-suffix">Ft</span>
                    </div>
                    <div class="field-error" id="price-error">Érvénytelen!</div>
                </div>
                <button type="submit" name="upload_item" class="submit-btn">Hirdetés feladása</button>
            </form>
        </div>
    </div>

    <!-- Edit Modal -->
    <div class="edit-modal" id="editModal">
        <div class="edit-modal-content">
            <div class="edit-modal-header">
                <h3 class="edit-modal-title">✏️ Hirdetés módosítása</h3>
                <button class="edit-modal-close" onclick="closeEditModal()">✕</button>
            </div>
            <?php if (isset($_GET['edit']) && $_GET['edit'] === 'success'): ?><div class="success-banner">✓ Mentve!</div><?php endif; ?>
            <form method="post" id="editForm">
                <input type="hidden" name="item_id" id="editItemId">
                <input type="hidden" name="edit_item" value="1">
                <div class="edit-form-group"><label class="edit-form-label">Cím</label><input class="edit-form-input" id="edit_title" name="edit_title"></div>
                <div class="edit-form-group"><label class="edit-form-label">Leírás</label><textarea class="edit-form-textarea" id="edit_description" name="edit_description"></textarea></div>
                <div class="edit-form-group"><label class="edit-form-label">Ár</label>
                    <div class="edit-price-wrapper"><input class="edit-form-input" id="edit_price" name="edit_price"><span class="edit-price-suffix">Ft</span></div>
                </div>
                <button type="submit" class="submit-btn">💾 Mentés</button>
            </form>
        </div>
    </div>

    <!-- Report Modal -->
    <div class="report-modal" id="reportModal">
        <div class="report-modal-content">
            <div class="report-modal-header">
                <span>⚠️</span>
                <h3 class="report-modal-title">Hirdetés bejelentése</h3>
                <button class="report-modal-close" onclick="closeReportModal()">✕</button>
            </div>
            <form method="post" id="reportForm">
                <input type="hidden" name="item_id" id="reportItemId">
                <input type="hidden" name="report_item" value="1">
                <textarea name="report_reason" class="form-textarea" placeholder="Részletezd a problémát..." required></textarea>
                <button type="submit" class="report-submit-btn">📢 Bejelentés</button>
            </form>
        </div>
    </div>

    <!-- Delete Confirm Modal -->
    <div class="delete-confirm-modal" id="deleteConfirmModal">
        <div class="delete-confirm-modal-content">
            <div class="delete-confirm-modal-header">
                <span class="delete-confirm-modal-icon">⚠️</span>
                <h3 class="delete-confirm-modal-title">Hirdetés törlése</h3>
                <button class="delete-confirm-modal-close" onclick="closeDeleteConfirmModal()">✕</button>
            </div>
            <div class="delete-confirm-modal-body">
                <p class="delete-confirm-modal-text">Biztosan törölni szeretnéd ezt a hirdetést?</p>
                <p class="delete-confirm-modal-warning">A törlés végleges, nem vonható vissza.</p>
            </div>
            <div class="delete-confirm-modal-actions">
                <button class="delete-confirm-cancel-btn" onclick="closeDeleteConfirmModal()">Mégse</button>
                <button class="delete-confirm-delete-btn" id="confirmDeleteBtn">Törlés</button>
            </div>
        </div>
    </div>

    <!-- Product Detail Modal -->
    <div class="product-modal-overlay" id="productModal">
        <div class="product-modal-card">
            <div class="product-modal-header">
                <div class="product-menu" id="productMenuContainer" style="display:none">
                    <div class="product-menu-button" onclick="toggleProductMenu(this)">⋮</div>
                    <div class="product-menu-content" id="productMenuContent">
                        <button class="product-menu-item" id="productEditBtn" style="display:none">✏️ Módosítás</button>
                        <button class="product-menu-item" id="productReportBtn">⚠️ Bejelentés</button>
                        <button class="product-menu-item delete" id="productDeleteBtn" style="display:none">🗑️ Törlés</button>
                    </div>
                </div>
                <button class="product-modal-close" id="closeProductModalBtn">✕</button>
            </div>
            <div class="product-gallery">
                <div class="product-main-image-container">
                    <img src="" class="product-main-image" id="productMainImage" style="display:none">
                    <div id="productNoImagePlaceholder" style="display:none">📷 Nincs kép</div>
                    <button class="gallery-nav prev" id="galleryPrev">❮</button>
                    <button class="gallery-nav next" id="galleryNext">❯</button>
                </div>
                <div class="product-thumbnails" id="productThumbnails"></div>
            </div>
            <div class="product-details">
                <h2 class="product-title" id="productTitle"></h2>
                <div class="product-price" id="productPrice"></div>
                <div class="product-seller" id="productSeller"></div>
                <div class="product-date" id="productDate"></div>
                <div class="product-description" id="productDescription"></div>
                <button class="product-buy-btn" id="productBuyBtn">🛒 Vásárlás</button>
            </div>
        </div>
    </div>

    <!-- Lightbox -->
    <div class="lightbox-overlay" id="lightboxOverlay">
        <div class="lightbox-content">
            <img src="" class="lightbox-image" id="lightboxImage">
            <button class="lightbox-close" id="lightboxClose">✕</button>
        </div>
    </div>

    <!-- Floating Messages -->
    <a href="uzenetek.php" class="floating-messages-btn" title="Üzenetek">
        💬
        <?php if ($unreadMsgCount > 0): ?><span class="floating-messages-badge" id="floatingMessagesBadge"><?= $unreadMsgCount > 9 ? '9+' : $unreadMsgCount ?></span>
        <?php else: ?><span class="floating-messages-badge" id="floatingMessagesBadge" style="display:none"></span><?php endif; ?>
    </a>

    <!-- Seller Profile Popup -->
    <div class="seller-popup-overlay" id="sellerPopupOverlay">
        <div class="seller-popup-card">
            <div class="seller-popup-topbar">
                <button class="seller-popup-close" id="sellerPopupClose">✕</button>
                <div class="seller-popup-topbar-title">👤 Eladó profilja</div>
            </div>
            <div class="seller-popup-body" id="sellerPopupContent">
                <div class="seller-popup-loading">⏳ Betöltés...</div>
            </div>
        </div>
    </div>

    <div class="main-content">
        <?php if (!empty($items)): ?>
            <div class="items-grid">
                <?php foreach ($items as $item):
                    $imageStmt = $conn->prepare("SELECT image_path FROM item_images WHERE item_id = ? AND is_primary = 1 LIMIT 1");
                    $imageStmt->execute([$item['id']]);
                    $primaryImage = $imageStmt->fetch(PDO::FETCH_ASSOC);
                    $countStmt = $conn->prepare("SELECT COUNT(*) as image_count FROM item_images WHERE item_id = ?");
                    $countStmt->execute([$item['id']]);
                    $imageCount = $countStmt->fetch(PDO::FETCH_ASSOC)['image_count'];
                    $allImagesStmt = $conn->prepare("SELECT image_path FROM item_images WHERE item_id = ? ORDER BY sort_order");
                    $allImagesStmt->execute([$item['id']]);
                    $allImages = $allImagesStmt->fetchAll(PDO::FETCH_COLUMN);
                    $isOwnerCard = ($item['user_id'] == $_SESSION['user_id']);
                ?>
                    <div class="item-card"
                        data-item-id="<?= $item['id'] ?>"
                        data-item-title="<?= htmlspecialchars($item['title']) ?>"
                        data-item-price="<?= number_format($item['price'], 0, ',', ' ') ?> Ft"
                        data-item-seller="<?= htmlspecialchars($item['seller_name']) ?>"
                        data-item-date="<?= date('Y-m-d', strtotime($item['created_at'])) ?>"
                        data-item-description="<?= htmlspecialchars($item['description']) ?>"
                        data-item-images='<?= json_encode($allImages) ?>'
                        data-item-user-id="<?= $item['user_id'] ?>">

                        <div class="card-menu">
                            <div class="card-menu-button" onclick="toggleMenu(this);event.stopPropagation()">⋮</div>
                            <div class="card-menu-content">
                                <?php if (!$isOwnerCard || $isAdmin): ?>
                                    <button class="card-menu-item" onclick="openReportModal('<?= $item['id'] ?>');event.stopPropagation()">⚠️ Bejelentés</button>
                                <?php endif; ?>
                                <?php if ($isOwnerCard || $isAdmin): ?>
                                    <button class="card-menu-item" onclick="openEditModal('<?= $item['id'] ?>',<?= htmlspecialchars(json_encode($item['title'])) ?>,<?= htmlspecialchars(json_encode($item['description'])) ?>,'<?= $item['price'] ?>');event.stopPropagation()">✏️ Módosítás</button>
                                    <button class="card-menu-item delete" onclick="openDeleteConfirmModal('<?= $item['id'] ?>');event.stopPropagation()">🗑️ Törlés</button>
                                <?php endif; ?>
                            </div>
                        </div>

                        <?php if ($primaryImage): ?>
                            <img src="<?= htmlspecialchars($primaryImage['image_path']) ?>" class="item-image">
                        <?php else: ?>
                            <div class="item-image-placeholder"><span class="placeholder-text">📷 Nincs kép</span></div>
                        <?php endif; ?>

                        <?php if ($imageCount > 1): ?><div class="image-count-badge">+<?= $imageCount - 1 ?> kép</div><?php endif; ?>
                        <div class="item-title"><?= htmlspecialchars($item['title']) ?></div>
                        <div class="item-price"><?= number_format($item['price'], 0, ',', ' ') ?> Ft</div>
                        <div class="item-seller" data-seller-id="<?= $item['user_id'] ?>" onclick="openSellerPopup(<?= $item['user_id'] ?>);event.stopPropagation()">Eladó: <?= htmlspecialchars($item['seller_name']) ?></div>
                        <div class="item-date"><?= date('Y-m-d', strtotime($item['created_at'])) ?></div>
                    </div>
                <?php endforeach; ?>
            </div>
            <?php if ($totalPages > 1): ?>
                <div class="floating-pagination">
                    <div class="pagination-container">
                        <?php if ($page > 1): ?><a href="?page=<?= $page - 1 ?>" class="pagination-btn">Előző</a><?php else: ?><span class="pagination-btn disabled">Előző</span><?php endif; ?>
                        <?php if ($page < $totalPages): ?><a href="?page=<?= $page + 1 ?>" class="pagination-btn">Következő</a><?php else: ?><span class="pagination-btn disabled">Következő</span><?php endif; ?>
                    </div>
                </div>
            <?php endif; ?>
        <?php endif; ?>
    </div>

    <script>
        let currentProductImages = [],
            currentImageIndex = 0,
            currentProductId = null,
            currentProductUserId = null,
            pendingDeleteItemId = null;
        const modal = document.getElementById('uploadModal'),
            openBtn = document.getElementById('openModalBtn'),
            closeBtn = document.getElementById('closeModalBtn');
        const imageInput = document.getElementById('item_images'),
            previewContainer = document.getElementById('imagePreview');
        let selectedFiles = [];

        function openModal() {
            modal.classList.add('active');
            document.body.style.overflow = 'hidden'
        }

        function closeModal() {
            modal.classList.remove('active');
            document.body.style.overflow = ''
        }
        openBtn.addEventListener('click', openModal);
        closeBtn.addEventListener('click', closeModal);
        modal.addEventListener('click', e => {
            if (e.target === modal) closeModal()
        });
        document.addEventListener('keydown', e => {
            if (e.key === 'Escape' && modal.classList.contains('active')) closeModal()
        });

        imageInput.addEventListener('change', function(e) {
            const files = Array.from(e.target.files);
            selectedFiles = files.filter(f => {
                if (!['image/jpeg', 'image/png', 'image/gif', 'image/webp'].includes(f.type)) {
                    alert(f.name + ' nem támogatott!');
                    return false
                }
                if (f.size > 5242880) {
                    alert(f.name + ' >5MB!');
                    return false
                }
                return true;
            });
            updatePreview();
        });

        function updatePreview() {
            previewContainer.innerHTML = '';
            selectedFiles.forEach((f, i) => {
                const r = new FileReader(),
                    item = document.createElement('div');
                item.className = 'image-preview-item';
                r.onload = function(e) {
                    item.innerHTML = `<img src="${e.target.result}"><div class="image-preview-remove" data-index="${i}">×</div>${i===0?'<div class="primary-badge">Főkép</div>':''}`;
                    item.querySelector('.image-preview-remove').addEventListener('click', function(e) {
                        e.stopPropagation();
                        removeImageAtIndex(parseInt(this.dataset.index))
                    });
                };
                r.readAsDataURL(f);
                previewContainer.appendChild(item);
            });
            validateImages();
        }

        function removeImageAtIndex(i) {
            selectedFiles.splice(i, 1);
            const d = new DataTransfer();
            selectedFiles.forEach(f => d.items.add(f));
            imageInput.files = d.files;
            updatePreview()
        }

        function validateImages() {
            const v = selectedFiles.length > 0;
            document.getElementById('images-error').style.display = v ? 'none' : 'block';
            document.querySelector('.image-upload-container').style.borderColor = v ? 'rgba(255,140,0,0.3)' : '#ff4d4d';
            return v;
        }

        const form = document.getElementById('uploadForm'),
            titleInput = document.getElementById('item_title'),
            descInput = document.getElementById('item_description'),
            priceInput = document.getElementById('item_price');

        function validateField(input, errId, cond) {
            const e = document.getElementById(errId);
            if (!cond) {
                input.classList.add('invalid');
                e.style.display = 'block';
                return false
            }
            input.classList.remove('invalid');
            e.style.display = 'none';
            return true
        }
        form.addEventListener('submit', e => {
            const d = new DataTransfer();
            selectedFiles.forEach(f => d.items.add(f));
            imageInput.files = d.files;
            let v = true;
            v = validateImages() && v;
            v = validateField(titleInput, 'title-error', titleInput.value.trim() !== '') && v;
            v = validateField(descInput, 'desc-error', descInput.value.trim() !== '') && v;
            v = validateField(priceInput, 'price-error', priceInput.value !== '' && parseFloat(priceInput.value) >= 0) && v;
            if (!v) e.preventDefault();
        });
        [titleInput, descInput, priceInput].forEach(el => el.addEventListener('input', () => el.classList.remove('invalid')));
        <?php if (isset($_GET['upload']) && $_GET['upload'] === 'success' || $uploadError): ?>openModal();
        <?php endif; ?>

        function toggleMenu(btn) {
            const m = btn.nextElementSibling;
            m.classList.toggle('show');
            document.querySelectorAll('.card-menu-content').forEach(x => {
                if (x !== m) x.classList.remove('show')
            })
        }
        document.addEventListener('click', e => {
            if (!e.target.closest('.card-menu')) document.querySelectorAll('.card-menu-content').forEach(m => m.classList.remove('show'))
        });

        const reportModal = document.getElementById('reportModal'),
            reportItemId = document.getElementById('reportItemId');

        function openReportModal(id) {
            reportItemId.value = id;
            reportModal.classList.add('show');
            document.body.style.overflow = 'hidden'
        }

        function closeReportModal() {
            reportModal.classList.remove('show');
            document.body.style.overflow = ''
        }
        reportModal.addEventListener('click', e => {
            if (e.target === reportModal) closeReportModal()
        });
        document.addEventListener('keydown', e => {
            if (e.key === 'Escape' && reportModal.classList.contains('show')) closeReportModal()
        });

        const deleteConfirmModal = document.getElementById('deleteConfirmModal'),
            confirmDeleteBtn = document.getElementById('confirmDeleteBtn');

        function openDeleteConfirmModal(itemId) {
            pendingDeleteItemId = itemId;
            deleteConfirmModal.classList.add('show');
            document.body.style.overflow = 'hidden'
        }

        function closeDeleteConfirmModal() {
            deleteConfirmModal.classList.remove('show');
            document.body.style.overflow = '';
            pendingDeleteItemId = null
        }
        confirmDeleteBtn.addEventListener('click', () => {
            if (pendingDeleteItemId) {
                const f = document.createElement('form');
                f.method = 'POST';
                f.innerHTML = `<input type="hidden" name="item_id" value="${pendingDeleteItemId}"><input type="hidden" name="delete_item" value="1">`;
                document.body.appendChild(f);
                f.submit()
            }
        });
        deleteConfirmModal.addEventListener('click', e => {
            if (e.target === deleteConfirmModal) closeDeleteConfirmModal()
        });
        document.addEventListener('keydown', e => {
            if (e.key === 'Escape' && deleteConfirmModal.classList.contains('show')) closeDeleteConfirmModal()
        });

        const productModal = document.getElementById('productModal'),
            closeProductModalBtn = document.getElementById('closeProductModalBtn');
        const productMainImage = document.getElementById('productMainImage'),
            lightboxOverlay = document.getElementById('lightboxOverlay'),
            lightboxImage = document.getElementById('lightboxImage');

        function setMainImage(i) {
            if (i >= 0 && i < currentProductImages.length && currentProductImages[i]) {
                productMainImage.style.display = 'block';
                document.getElementById('productNoImagePlaceholder').style.display = 'none';
                productMainImage.src = currentProductImages[i];
                currentImageIndex = i;
                productMainImage.onload = adjustImageContainerHeight;
                productMainImage.onerror = () => {
                    productMainImage.style.display = 'none';
                    document.getElementById('productNoImagePlaceholder').style.display = 'block'
                };
                document.querySelectorAll('.product-thumbnail').forEach((t, idx) => t.classList.toggle('active', idx === i));
            } else {
                productMainImage.style.display = 'none';
                document.getElementById('productNoImagePlaceholder').style.display = 'block'
            }
        }

        function adjustImageContainerHeight() {
            const c = document.querySelector('.product-main-image-container'),
                g = document.querySelector('.product-gallery'),
                t = document.querySelector('.product-thumbnails');
            if (c && g) {
                const avail = g.clientHeight - 32 - (t ? t.offsetHeight : 100) - 20;
                c.style.height = Math.max(200, avail) + 'px'
            }
        }

        document.querySelectorAll('.item-card').forEach(card => {
            card.addEventListener('click', function(e) {
                if (e.target.closest('.card-menu') || e.target.closest('.report-modal')) return;
                const id = this.dataset.itemId,
                    title = this.dataset.itemTitle,
                    price = this.dataset.itemPrice,
                    seller = this.dataset.itemSeller;
                const date = this.dataset.itemDate,
                    desc = this.dataset.itemDescription,
                    images = JSON.parse(this.dataset.itemImages || '[]'),
                    uid = this.dataset.itemUserId;
                currentProductId = id;
                currentProductUserId = uid;
                currentProductImages = images;
                currentImageIndex = 0;
                document.getElementById('productTitle').textContent = title;
                document.getElementById('productPrice').textContent = price;
                document.getElementById('productSeller').innerHTML = `Eladó: <strong>${seller}</strong>`;
                document.getElementById('productSeller').setAttribute('data-seller-id', uid);
                document.getElementById('productDate').textContent = date;
                document.getElementById('productDescription').textContent = desc;
                const tc = document.getElementById('productThumbnails');
                tc.innerHTML = '';
                if (images.length > 0) {
                    images.forEach((img, i) => {
                        const tn = document.createElement('div');
                        tn.className = `product-thumbnail ${i===0?'active':''}`;
                        tn.innerHTML = `<img src="${img}">`;
                        tn.addEventListener('click', e => {
                            e.stopPropagation();
                            setMainImage(i)
                        });
                        tc.appendChild(tn)
                    });
                    setMainImage(0)
                } else setMainImage(-1);
                document.getElementById('galleryPrev').classList.toggle('hidden', images.length <= 1);
                document.getElementById('galleryNext').classList.toggle('hidden', images.length <= 1);
                const menu = document.getElementById('productMenuContainer'),
                    rb = document.getElementById('productReportBtn'),
                    db = document.getElementById('productDeleteBtn'),
                    eb = document.getElementById('productEditBtn');
                const isOwner = parseInt(uid) === <?= $_SESSION['user_id'] ?>,
                    isAdmin = <?= $isAdmin ? 'true' : 'false' ?>;
                menu.style.display = 'block';
                if (!isOwner || isAdmin) {
                    rb.style.display = 'block';
                    rb.onclick = () => openReportModal(id)
                } else rb.style.display = 'none';
                if (isOwner || isAdmin) {
                    eb.style.display = 'block';
                    eb.onclick = () => openEditModal(id, title, desc, price.replace(/[^0-9]/g, ''));
                    db.style.display = 'block';
                    db.onclick = () => openDeleteConfirmModal(id)
                } else {
                    eb.style.display = 'none';
                    db.style.display = 'none'
                }
                document.getElementById('productBuyBtn').onclick = () => {
                    window.location.href = 'vasarlas.php?item_id=' + id
                };
                openProductModal();
            });
        });

        function openProductModal() {
            productModal.classList.add('active');
            document.body.style.overflow = 'hidden';
            setTimeout(adjustImageContainerHeight, 100)
        }

        function closeProductModal() {
            if (lightboxOverlay.classList.contains('active')) lightboxOverlay.classList.remove('active');
            productModal.classList.remove('active');
            document.body.style.overflow = ''
        }
        document.getElementById('galleryPrev').addEventListener('click', e => {
            e.stopPropagation();
            setMainImage((currentImageIndex - 1 + currentProductImages.length) % currentProductImages.length)
        });
        document.getElementById('galleryNext').addEventListener('click', e => {
            e.stopPropagation();
            setMainImage((currentImageIndex + 1) % currentProductImages.length)
        });
        closeProductModalBtn.addEventListener('click', closeProductModal);
        productModal.addEventListener('click', e => {
            if (e.target === productModal) closeProductModal()
        });
        document.addEventListener('keydown', e => {
            if (e.key === 'Escape' && productModal.classList.contains('active')) closeProductModal()
        });
        productMainImage.addEventListener('click', e => {
            e.stopPropagation();
            if (productMainImage.src && productMainImage.style.display !== 'none') {
                lightboxImage.src = productMainImage.src;
                lightboxOverlay.classList.add('active')
            }
        });
        document.getElementById('lightboxClose').addEventListener('click', () => lightboxOverlay.classList.remove('active'));
        lightboxOverlay.addEventListener('click', e => {
            if (e.target === lightboxOverlay) lightboxOverlay.classList.remove('active')
        });
        document.addEventListener('keydown', e => {
            if (e.key === 'Escape' && lightboxOverlay.classList.contains('active')) lightboxOverlay.classList.remove('active')
        });
        window.addEventListener('resize', () => {
            if (productModal.classList.contains('active')) adjustImageContainerHeight()
        });

        (function() {
            const cb = document.getElementById('themeSwitchMain'),
                tl = document.getElementById('themeStylesheet');

            function apply(t) {
                tl.href = t === 'light' ? 'theme-light.css?v=2' : 'theme-dark.css?v=2';
                localStorage.setItem('theme', t);
                cb.checked = t === 'light';
                document.documentElement.setAttribute('data-theme', t);
                document.body.setAttribute('data-theme', t)
            }
            apply(localStorage.getItem('theme') || 'dark');
            cb.addEventListener('change', () => apply(cb.checked ? 'light' : 'dark'));
        })();

        const si = document.getElementById('searchInput'),
            sr = document.getElementById('searchResults');
        let st;

        function performSearch() {
            const q = si.value.trim();
            if (q.length < 2) {
                sr.classList.remove('show');
                return
            }
            fetch(`?search_query=${encodeURIComponent(q)}`).then(r => r.json()).then(d => {
                if (d.error) return;
                if (d.length === 0) {
                    sr.classList.remove('show');
                    return
                }
                const lm = document.body.getAttribute('data-theme') === 'light',
                    pc = lm ? '#7a9200' : '#ff8c00';
                const ps = `data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 24 24' fill='${encodeURIComponent(pc)}'%3E%3Cpath d='M4 4h16v2H4V4zm2 4h12v2H6V8zm14-4v16H4V4h16z'/%3E%3C/svg%3E`;
                sr.innerHTML = d.map(i => `<div class="search-result-item" data-item-id="${i.id}"><img src="${i.primary_image||''}" class="search-result-image" onerror="this.src='${ps}'"><div class="search-result-info"><div class="search-result-title">${escapeHtml(i.title)}</div><div class="search-result-price">${Number(i.price).toLocaleString('hu-HU')} Ft</div><div class="search-result-seller">${escapeHtml(i.seller_name)}</div></div></div>`).join('');
                sr.classList.add('show');
                document.querySelectorAll('.search-result-item').forEach(el => el.addEventListener('click', () => fetchItemDetails(el.dataset.itemId)));
            });
        }
        si.addEventListener('input', () => {
            clearTimeout(st);
            st = setTimeout(performSearch, 300)
        });
        document.addEventListener('click', e => {
            if (!si.contains(e.target) && !sr.contains(e.target)) sr.classList.remove('show')
        });

        function fetchItemDetails(id) {
            fetch(`?get_item=${id}`).then(r => r.json()).then(d => {
                if (d.error) return;
                currentProductId = d.id;
                currentProductUserId = d.user_id;
                currentProductImages = d.images;
                currentImageIndex = 0;
                document.getElementById('productTitle').textContent = d.title;
                document.getElementById('productPrice').textContent = `${Number(d.price).toLocaleString('hu-HU')} Ft`;
                document.getElementById('productSeller').innerHTML = `Eladó: <strong>${escapeHtml(d.seller_name)}</strong>`;
                document.getElementById('productSeller').setAttribute('data-seller-id', d.user_id);
                document.getElementById('productDate').textContent = d.created_at.substring(0, 10);
                document.getElementById('productDescription').textContent = d.description;
                const tc = document.getElementById('productThumbnails');
                tc.innerHTML = '';
                if (d.images && d.images.length) {
                    d.images.forEach((img, i) => {
                        const tn = document.createElement('div');
                        tn.className = `product-thumbnail ${i===0?'active':''}`;
                        tn.innerHTML = `<img src="${img}">`;
                        tn.addEventListener('click', e => {
                            e.stopPropagation();
                            setMainImage(i)
                        });
                        tc.appendChild(tn)
                    });
                    setMainImage(0)
                } else setMainImage(-1);
                document.getElementById('galleryPrev').classList.toggle('hidden', !d.images || d.images.length <= 1);
                document.getElementById('galleryNext').classList.toggle('hidden', !d.images || d.images.length <= 1);
                const menu = document.getElementById('productMenuContainer'),
                    rb = document.getElementById('productReportBtn'),
                    db = document.getElementById('productDeleteBtn'),
                    eb = document.getElementById('productEditBtn');
                const isOwner = parseInt(d.user_id) === <?= $_SESSION['user_id'] ?>,
                    isAdmin = <?= $isAdmin ? 'true' : 'false' ?>;
                menu.style.display = 'block';
                if (!isOwner || isAdmin) {
                    rb.style.display = 'block';
                    rb.onclick = () => openReportModal(d.id)
                } else rb.style.display = 'none';
                if (isOwner || isAdmin) {
                    eb.style.display = 'block';
                    eb.onclick = () => openEditModal(d.id, d.title, d.description, d.price);
                    db.style.display = 'block';
                    db.onclick = () => openDeleteConfirmModal(d.id)
                } else {
                    eb.style.display = 'none';
                    db.style.display = 'none'
                }
                document.getElementById('productBuyBtn').onclick = () => {
                    window.location.href = 'vasarlas.php?item_id=' + d.id
                };
                openProductModal();
            });
        }

        function toggleProductMenu(btn) {
            const m = btn.nextElementSibling;
            m.classList.toggle('show');
            document.querySelectorAll('.product-menu-content').forEach(x => {
                if (x !== m) x.classList.remove('show')
            })
        }

        const editModal = document.getElementById('editModal');

        function openEditModal(id, title, desc, price) {
            document.getElementById('editItemId').value = id;
            document.getElementById('edit_title').value = title;
            document.getElementById('edit_description').value = desc;
            document.getElementById('edit_price').value = parseFloat(price) || price;
            editModal.classList.add('show');
            document.body.style.overflow = 'hidden'
        }

        function closeEditModal() {
            editModal.classList.remove('show');
            document.body.style.overflow = ''
        }
        editModal.addEventListener('click', e => {
            if (e.target === editModal) closeEditModal()
        });
        document.addEventListener('keydown', e => {
            if (e.key === 'Escape' && editModal.classList.contains('show')) closeEditModal()
        });

        function escapeHtml(s) {
            return s ? s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;') : ''
        }

        const sellerOverlay = document.getElementById('sellerPopupOverlay'),
            sellerContent = document.getElementById('sellerPopupContent');

        function openSellerPopup(sid) {
            sellerContent.innerHTML = '<div class="seller-popup-loading">⏳ Betöltés...</div>';
            sellerOverlay.style.display = 'flex';
            sellerOverlay.offsetHeight;
            sellerOverlay.classList.add('active');
            document.body.style.overflow = 'hidden';
            fetch(`?get_seller=${sid}`).then(r => r.json()).then(d => {
                if (d.error) {
                    sellerContent.innerHTML = `<p style="color:red;text-align:center;padding:2rem">${escapeHtml(d.error)}</p>`;
                    return
                }
                const currentUserId = <?= (int)$_SESSION['user_id'] ?>,
                    memberSince = d.created_at ? d.created_at.substring(0, 10) : '—';
                const adminBadge = parseInt(d.is_admin) ? ' <span class="admin-badge">Admin</span>' : '',
                    initial = d.username ? d.username.charAt(0).toUpperCase() : '?';
                let avatarHtml = d.profile_picture && d.profile_picture.trim() ? `<img src="${escapeHtml(d.profile_picture)}" class="seller-popup-avatar-img">` : initial;
                let itemsHtml = '';
                if (d.latest_items && d.latest_items.length) {
                    itemsHtml = `<div class="seller-popup-items-title">Hirdetések</div><div class="seller-popup-items-grid">`;
                    d.latest_items.forEach(it => {
                        const imgHtml = it.thumb ? `<img src="${escapeHtml(it.thumb)}" onerror="this.style.display='none';this.nextElementSibling.style.display='flex'"><div class="seller-item-thumb-placeholder" style="display:none">📷</div>` : '<div class="seller-item-thumb-placeholder">📷</div>';
                        itemsHtml += `<div class="seller-item-thumb" onclick="closeSellerPopup();fetchItemDetails('${escapeHtml(it.id)}')">${imgHtml}<div class="seller-item-info"><div class="seller-item-title">${escapeHtml(it.title)}</div><div class="seller-item-price">${Number(it.price).toLocaleString('hu-HU')} Ft</div></div></div>`;
                    });
                    itemsHtml += '</div>';
                }
                const msgBtn = parseInt(sid) !== currentUserId ? `<a href="uzenetek.php?with=${sid}" class="seller-popup-msg-btn">💬 Üzenet küldése</a>` : `<div style="text-align:center;color:rgba(255,255,255,0.3);padding:1rem 0">Ez a saját profilod</div>`;
                sellerContent.innerHTML = `<div class="seller-popup-avatar" style="display:flex;align-items:center;justify-content:center">${avatarHtml}</div><div class="seller-popup-name">${escapeHtml(d.username)}${adminBadge}</div><div class="seller-popup-meta">Tag azóta: ${memberSince}</div><div class="seller-popup-stats"><div class="seller-stat"><div class="seller-stat-value">${d.item_count}</div><div class="seller-stat-label">Hirdetés</div></div></div>${itemsHtml}${msgBtn}`;
            }).catch(() => {
                sellerContent.innerHTML = '<p style="color:red;text-align:center;padding:2rem">Hálózati hiba</p>'
            });
        }

        function closeSellerPopup() {
            sellerOverlay.classList.remove('active');
            document.body.style.overflow = '';
            setTimeout(() => {
                sellerOverlay.style.display = 'none'
            }, 300)
        }
        document.getElementById('sellerPopupClose').addEventListener('click', closeSellerPopup);
        sellerOverlay.addEventListener('click', e => {
            if (e.target === sellerOverlay) closeSellerPopup()
        });
        document.addEventListener('keydown', e => {
            if (e.key === 'Escape' && sellerOverlay.classList.contains('active')) closeSellerPopup()
        });
        document.getElementById('productSeller').addEventListener('click', function() {
            const s = this.getAttribute('data-seller-id');
            if (s) openSellerPopup(s)
        });

        const amb = document.getElementById('accountMenuBtn'),
            ad = document.getElementById('accountDropdown');

        function closeDropdown() {
            ad.classList.remove('show')
        }
        amb.addEventListener('click', e => {
            e.stopPropagation();
            ad.classList.toggle('show')
        });
        ad.addEventListener('click', e => e.stopPropagation());
        document.addEventListener('click', closeDropdown);

        let lastUnreadCount = <?= $unreadMsgCount ?>;
        const msgBadge = document.getElementById('floatingMessagesBadge');
        let toastMsg = document.getElementById('messageToast');
        if (!toastMsg) {
            toastMsg = document.createElement('div');
            toastMsg.id = 'messageToast';
            toastMsg.style.cssText = 'position:fixed;bottom:100px;right:30px;z-index:9999;background:var(--orange-bright);color:#000;padding:12px 20px;border-radius:50px;box-shadow:0 8px 25px rgba(0,0,0,0.5);font-weight:bold;cursor:pointer;transition:all 0.3s;opacity:0;transform:translateY(20px);pointer-events:none';
            document.body.appendChild(toastMsg);
            toastMsg.addEventListener('click', () => {
                window.location.href = 'uzenetek.php'
            })
        }

        function showToast(s, p) {
            toastMsg.textContent = `💬 Új üzenet: ${s} - "${p}"`;
            toastMsg.style.opacity = '1';
            toastMsg.style.transform = 'translateY(0)';
            toastMsg.style.pointerEvents = 'auto';
            setTimeout(() => {
                toastMsg.style.opacity = '0';
                toastMsg.style.transform = 'translateY(20px)';
                toastMsg.style.pointerEvents = 'none'
            }, 5000)
        }
        async function checkUnread() {
            try {
                const r = await fetch('?get_unread_count=1'),
                    d = await r.json();
                if (d.error) return;
                if (d.unread_count > 0) {
                    msgBadge.textContent = d.unread_count > 9 ? '9+' : d.unread_count;
                    msgBadge.style.display = 'flex'
                } else msgBadge.style.display = 'none';
                if (d.unread_count > lastUnreadCount && d.last_message) showToast(d.last_message.sender, d.last_message.preview);
                lastUnreadCount = d.unread_count
            } catch (e) {}
        }
        let upi = setInterval(checkUnread, 15000);
        document.addEventListener('visibilitychange', () => {
            if (document.hidden) {
                clearInterval(upi);
                upi = setInterval(checkUnread, 60000)
            } else {
                clearInterval(upi);
                upi = setInterval(checkUnread, 15000);
                checkUnread()
            }
        });
        checkUnread();
    </script>
</body>

</html>