-- phpMyAdmin SQL Dump
-- version 5.2.2deb1+deb13u1
-- https://www.phpmyadmin.net/
--
-- Gép: localhost:3306
-- Létrehozás ideje: 2026. Máj 21. 16:36
-- Kiszolgáló verziója: 11.8.3-MariaDB-0+deb13u1 from Debian
-- PHP verzió: 8.4.16

SET SQL_MODE = "NO_AUTO_VALUE_ON_ZERO";
START TRANSACTION;
SET time_zone = "+00:00";


/*!40101 SET @OLD_CHARACTER_SET_CLIENT=@@CHARACTER_SET_CLIENT */;
/*!40101 SET @OLD_CHARACTER_SET_RESULTS=@@CHARACTER_SET_RESULTS */;
/*!40101 SET @OLD_COLLATION_CONNECTION=@@COLLATION_CONNECTION */;
/*!40101 SET NAMES utf8mb4 */;

--
-- Adatbázis: `cuci_ady_pepa_hu`
--

-- --------------------------------------------------------

--
-- Tábla szerkezet ehhez a táblához `admins`
--

CREATE TABLE `admins` (
  `user_id` int(11) NOT NULL,
  `assigned_at` timestamp NULL DEFAULT current_timestamp()
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

--
-- A tábla adatainak kiíratása `admins`
--

INSERT INTO `admins` (`user_id`, `assigned_at`) VALUES
(1, '2026-04-16 07:40:39');

-- --------------------------------------------------------

--
-- Tábla szerkezet ehhez a táblához `hidden_conversations`
--

CREATE TABLE `hidden_conversations` (
  `user_id` int(11) NOT NULL,
  `partner_id` int(11) NOT NULL,
  `hidden_at` timestamp NULL DEFAULT current_timestamp()
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

--
-- A tábla adatainak kiíratása `hidden_conversations`
--

INSERT INTO `hidden_conversations` (`user_id`, `partner_id`, `hidden_at`) VALUES
(1, 7, '2026-04-27 11:43:21');

-- --------------------------------------------------------

--
-- Tábla szerkezet ehhez a táblához `items`
--

CREATE TABLE `items` (
  `id` char(12) NOT NULL,
  `user_id` int(11) NOT NULL,
  `title` varchar(255) NOT NULL,
  `description` text DEFAULT NULL,
  `price` decimal(10,2) NOT NULL,
  `created_at` timestamp NULL DEFAULT current_timestamp(),
  `updated_at` timestamp NULL DEFAULT current_timestamp() ON UPDATE current_timestamp(),
  `sold` tinyint(1) DEFAULT 0
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

--
-- A tábla adatainak kiíratása `items`
--

INSERT INTO `items` (`id`, `user_id`, `title`, `description`, `price`, `created_at`, `updated_at`, `sold`) VALUES
('0F9pInUbugyG', 7, 'eladó súlyzókészlet', 'nagy izmokat lehet vele építeni', 60000.00, '2026-04-22 09:48:46', '2026-04-28 08:24:44', 0),
('13GYVNqEmZiM', 7, 'eladó mikrofon', 'eladó szép mikrofon', 230000.00, '2026-04-22 09:17:21', '2026-04-22 09:25:08', 0),
('1YNK5TROEk9c', 1, 'Zongora', 'Nagyon jó állapotban lévő, nagyon jó minőségű, eladó nem lopott', 650000.00, '2026-04-16 07:53:25', '2026-04-16 07:53:25', 0),
('2AFxdybwULag', 3, 'eladó sál', 'meleg', 2500.00, '2026-04-22 10:00:13', '2026-04-22 10:00:13', 0),
('2vyiHaSsVQAU', 7, 'eladó telefon', 'csere érdekel.', 10000.00, '2026-04-22 09:11:48', '2026-04-22 09:11:48', 0),
('3QhE2TsaADr6', 7, 'eladó akvárium', 'szép és jó állapotú', 60000.00, '2026-04-22 09:44:00', '2026-04-22 09:44:00', 0),
('4C5Ac78FQrEo', 3, 'elektromos fogkefe szett', 'kimossa a fogaidat', 23456.00, '2026-04-22 10:15:39', '2026-04-27 11:37:21', 0),
('58nZ049eKtsB', 3, 'gőzölős vasaló', 'gőzöl mint a gőzmozdony és kivasalja a ruhát', 18950.00, '2026-04-22 10:12:25', '2026-04-22 10:12:25', 0),
('6J3mHFzRSU5c', 7, 'eladó ágy kutyának', 'kutyáknak való', 23000.00, '2026-04-22 09:19:11', '2026-04-27 11:37:30', 0),
('7OqUcxNwVbk2', 3, 'bicikli', 'lehet vele egykerekezni', 76582.00, '2026-04-22 10:12:48', '2026-04-22 10:12:48', 0),
('B5S6TKeu8dzN', 7, 'eladó opel', 'kicsit csörög de megy', 670000.00, '2026-04-22 09:23:07', '2026-04-22 09:23:31', 0),
('BmaUIHkpAl8b', 3, 'robot porszívó', 'eladó magátol megy és felszívja a retket', 100000.00, '2026-04-22 10:10:59', '2026-04-22 10:10:59', 0),
('BroNfI6Zse5Q', 7, 'eladó mosógép', 'kicsit csapágyas', 10000.00, '2026-04-22 09:17:41', '2026-04-22 09:17:41', 0),
('cxYD7ZTtSbif', 1, 'Bordó Öltöny', 'Eladó bordó öltöny. használt állapotban, jó minőségben', 50000.00, '2026-04-20 07:17:44', '2026-04-20 07:17:44', 0),
('dmcfwF0xBRug', 7, 'eladó modern technika kézikönyv', 'eladó', 20000.00, '2026-04-22 09:12:25', '2026-04-22 09:12:25', 0),
('DQyRINire4qs', 3, 'eladó pd elemek', 'nagyot lép vele az 1.9', 325675.00, '2026-04-22 10:14:02', '2026-04-22 10:14:02', 0),
('eoghnOHJiYm3', 7, 'méretes fikusz növény', 'szép nagy és zöld', 8500.00, '2026-04-22 09:50:55', '2026-04-22 09:50:55', 0),
('Fe7wz6lJK13I', 3, 'futógép eladó', 'gyorsan lehet rajta futni', 235000.00, '2026-04-22 09:57:43', '2026-04-22 09:57:43', 0),
('Gao98dkuqWL5', 1, 'PlayStation 5', 'pléjsztésön phejj karoj sümmeg gyaa, szarul fut', 250000.00, '2026-04-16 07:57:32', '2026-04-16 07:57:32', 0),
('GdXme0bzr1Bv', 3, 'eladó csilimbulátor', 'szegélynyíró', 260000.00, '2026-04-22 10:02:25', '2026-04-22 10:05:53', 0),
('H16OBEtbn3Rs', 7, 'bélyeggyűjtemény', 'teli pack', 12500.00, '2026-04-22 09:46:09', '2026-04-22 09:46:09', 0),
('HNsPAYRQyBxn', 7, 'eladó hegedű', 'feszes húrokkal', 230000.00, '2026-04-22 09:41:12', '2026-04-22 09:41:29', 0),
('HVTXOzRtM1rq', 7, 'eladó kanapé', 'kanapé', 130000.00, '2026-04-22 09:16:13', '2026-04-22 09:22:54', 0),
('i0kmFKh2gaDf', 7, 'elado lego', 'rakd össze', 230000.00, '2026-04-22 09:20:56', '2026-04-22 09:20:56', 0),
('IJK87PGuqgsi', 3, 'eladfó csillár', 'szépen világít', 94500.00, '2026-04-22 10:11:52', '2026-04-22 10:11:52', 0),
('jk4mJ8qwXasp', 7, 'téli sapka eladó', 'jó meleg', 2000.00, '2026-04-22 09:49:23', '2026-04-22 09:49:23', 0),
('khSPmQLDuyaF', 7, 'eladó babakocsi', 'babákat lehet benne tologatni', 25000.00, '2026-04-22 09:43:25', '2026-04-22 09:43:25', 0),
('kYroenTfUFRD', 1, 'Atomerőművek', 'Könyv', 15000.00, '2026-04-17 10:37:02', '2026-04-17 10:37:02', 0),
('l2iGSwQHT67e', 7, 'eladó szakácskönyv', 'helyileg debrecen', 10000.00, '2026-04-22 09:13:17', '2026-04-22 09:13:17', 0),
('LaivpEm3TtG8', 7, 'társasjáték eladó', 'csoportos társas', 4000.00, '2026-04-22 09:26:21', '2026-04-22 09:26:21', 0),
('ofzFGvQI015m', 7, 'eladó vérnyomás mérő', 'mindig jót mutat', 32000.00, '2026-04-22 09:18:50', '2026-04-22 09:18:50', 0),
('pm1K5MDdLRfr', 7, 'hajformázó eladó', 'kiváló állapotú', 10000.00, '2026-04-22 09:24:46', '2026-04-22 09:24:46', 0),
('pudTo7Ex8qsc', 2, 'Volkswagen Phaeton', 'Ha egy olyan autót keresel, ami nem hivalkodó, mégis minden porcikájában prémium érzetet ad, akkor a Volkswagen Phaeton pontosan neked való.\r\n\r\nEz a modell a Volkswagen mérnöki tudásának csúcsa volt: olyan szintű komforttal és minőséggel készült, ami simán felveszi a versenyt a nagy német luxusmárkákkal. Masszív felépítés, kiváló zajszigetelés és elképesztően stabil úttartás jellemzi – autópályán szinte lebeg.\r\n\r\nFőbb jellemzők:\r\n\r\nPrémium belső tér (bőr, fa, finom anyagok)\r\nRendkívül csendes utastér\r\nErős, megbízható motorválaszték\r\nLégrugós futómű a maximális komfortért\r\nDiszkrét megjelenés – aki ért hozzá, tudja mit lát\r\n\r\nEz az autó nem villogni akar, hanem kényelmesen és stílusosan eljuttatni A-ból B-be – minden egyes alkalommal első osztályon.', 12000000.00, '2026-04-22 09:13:06', '2026-04-28 08:02:54', 0),
('qPhW5H9VgC2r', 2, 'Futópad', 'Jó', 50000.00, '2026-04-16 07:42:21', '2026-04-16 07:42:21', 0),
('qW6sAX8gpjKn', 7, 'elektromos sövényvágó', 'szépen lehet vele nyírni a sövényt. hobbi használatra alkalmas', 25000.00, '2026-04-22 09:52:41', '2026-04-22 09:52:41', 0),
('SD6TbiH7VkgL', 7, 'akusztikus gitár eladó', 'feszesek a húrok rajta jó állapotú', 15000.00, '2026-04-22 09:44:57', '2026-04-22 09:44:57', 0),
('sW1ZUvcx3b7g', 7, 'gördeszka eladó', 'jó nagyot lehet esni vele', 5000.00, '2026-04-22 09:46:35', '2026-04-22 09:46:35', 0),
('t3kYFhfJN95B', 7, 'eladó horgászbot orsóval', 'hibátlan állapot nagy halakat lehet vele fogni', 32500.00, '2026-04-22 09:45:29', '2026-04-22 09:45:29', 0),
('uFIQw9NJERmS', 3, 'eladó hajszárító', 'szárítja a hajad nagyon hatékony', 12500.00, '2026-04-22 09:54:31', '2026-04-22 09:54:31', 0),
('urgcqztaFTiD', 3, 'erősítő eladó', 'szépen szólnak a gitárok rajta', 45000.00, '2026-04-22 09:59:07', '2026-04-22 09:59:07', 0),
('VaAbimox3Qtw', 7, 'dupla monitor eladó', 'állítható magasságú', 20000.00, '2026-04-22 09:48:08', '2026-04-22 09:48:08', 0),
('VQAGXDTEv7pS', 7, 'robot fűnyíró', 'eladó olyan fűnyíró ami magátol vagja a füvet', 1200000.00, '2026-04-22 09:16:49', '2026-04-22 09:16:49', 0),
('vydtqBe4ua5c', 7, 'baba kamera', 'baba kamera 1x használt', 20000.00, '2026-04-22 09:18:24', '2026-04-22 09:54:44', 0),
('w8ToCVufvsyW', 7, 'szintetizátor eladó', 'zenészeknek', 90000.00, '2026-04-22 09:44:22', '2026-04-22 09:44:22', 0),
('wg13C0H9piXx', 7, 'eladó kabát', 'Eladó női kabát', 9900.00, '2026-04-22 09:19:34', '2026-04-22 09:24:29', 0),
('wYQeKZVOsrgy', 1, 'Adidas Cipő', 'Adidas Cipő, kínai, kicsit kopott', 45000.00, '2026-04-16 07:53:52', '2026-04-16 07:53:52', 0),
('xgk0bKo6Q1J5', 7, 'gamer szék', 'jó állapotú', 20000.00, '2026-04-22 09:25:55', '2026-04-22 09:25:55', 0),
('XumC50VOfIkl', 7, 'cukor daráló', 'eladó daráló', 13000.00, '2026-04-22 09:14:22', '2026-04-22 09:14:22', 0),
('xUTCa64wAtuR', 3, 'fém polcrendszer', 'tárolásra alkalmas', 10000.00, '2026-04-22 09:55:03', '2026-04-22 09:55:03', 0),
('XV9F0QzjaIyl', 1, 'Napszemüveg', 'jó', 3500.00, '2026-04-16 08:00:00', '2026-04-16 08:00:00', 0),
('yXvGoNBnH8Im', 7, 'eladó kamera', 'ebbe jól nézel ki(:', 4900.00, '2026-04-22 09:19:52', '2026-04-22 09:22:09', 0),
('zjVx6oQhCimd', 3, 'eladó arcápoló', 'szép lesz az arcod tőle', 4500.00, '2026-04-22 09:56:50', '2026-04-22 09:56:50', 0);

-- --------------------------------------------------------

--
-- Tábla szerkezet ehhez a táblához `item_images`
--

CREATE TABLE `item_images` (
  `id` int(11) NOT NULL,
  `item_id` char(12) NOT NULL,
  `image_path` varchar(255) NOT NULL,
  `image_filename` varchar(255) NOT NULL,
  `is_primary` tinyint(1) DEFAULT 0,
  `sort_order` int(11) DEFAULT 0,
  `uploaded_at` timestamp NULL DEFAULT current_timestamp()
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

--
-- A tábla adatainak kiíratása `item_images`
--

INSERT INTO `item_images` (`id`, `item_id`, `image_path`, `image_filename`, `is_primary`, `sort_order`, `uploaded_at`) VALUES
(1, 'qPhW5H9VgC2r', 'uploads/qPhW5H9VgC2r/69e092dd0bd0e_0.jpg', '69e092dd0bd0e_0.jpg', 1, 0, '2026-04-16 07:42:21'),
(2, '1YNK5TROEk9c', 'uploads/1YNK5TROEk9c/69e09575bd061_0.jpg', '69e09575bd061_0.jpg', 1, 0, '2026-04-16 07:53:25'),
(3, 'wYQeKZVOsrgy', 'uploads/wYQeKZVOsrgy/69e09590060e1_0.jpg', '69e09590060e1_0.jpg', 1, 0, '2026-04-16 07:53:52'),
(4, 'Gao98dkuqWL5', 'uploads/Gao98dkuqWL5/69e0966d010fe_0.jpg', '69e0966d010fe_0.jpg', 1, 0, '2026-04-16 07:57:33'),
(5, 'XV9F0QzjaIyl', 'uploads/XV9F0QzjaIyl/69e09700a4c0b_0.jpg', '69e09700a4c0b_0.jpg', 1, 0, '2026-04-16 08:00:00'),
(6, 'kYroenTfUFRD', 'uploads/kYroenTfUFRD/69e20d4e61d62_0.jpg', '69e20d4e61d62_0.jpg', 1, 0, '2026-04-17 10:37:02'),
(11, 'cxYD7ZTtSbif', 'uploads/cxYD7ZTtSbif/69e5d318d8a01_0.webp', '69e5d318d8a01_0.webp', 1, 0, '2026-04-20 07:17:44'),
(15, '2vyiHaSsVQAU', 'uploads/2vyiHaSsVQAU/69e890d4d3ac0_0.jpg', '69e890d4d3ac0_0.jpg', 1, 0, '2026-04-22 09:11:48'),
(16, 'dmcfwF0xBRug', 'uploads/dmcfwF0xBRug/69e890f9960b9_0.jpg', '69e890f9960b9_0.jpg', 1, 0, '2026-04-22 09:12:25'),
(17, 'pudTo7Ex8qsc', 'uploads/pudTo7Ex8qsc/69e8912235eec_0.jpg', '69e8912235eec_0.jpg', 1, 0, '2026-04-22 09:13:06'),
(18, 'l2iGSwQHT67e', 'uploads/l2iGSwQHT67e/69e8912dae330_0.jpg', '69e8912dae330_0.jpg', 1, 0, '2026-04-22 09:13:17'),
(19, 'XumC50VOfIkl', 'uploads/XumC50VOfIkl/69e8916e3a5a3_0.jpg', '69e8916e3a5a3_0.jpg', 1, 0, '2026-04-22 09:14:22'),
(20, 'HVTXOzRtM1rq', 'uploads/HVTXOzRtM1rq/69e891ddac527_0.jpg', '69e891ddac527_0.jpg', 1, 0, '2026-04-22 09:16:13'),
(21, 'VQAGXDTEv7pS', 'uploads/VQAGXDTEv7pS/69e892016bc4a_0.jpg', '69e892016bc4a_0.jpg', 1, 0, '2026-04-22 09:16:49'),
(22, '13GYVNqEmZiM', 'uploads/13GYVNqEmZiM/69e89221be82c_0.jpg', '69e89221be82c_0.jpg', 1, 0, '2026-04-22 09:17:21'),
(23, 'BroNfI6Zse5Q', 'uploads/BroNfI6Zse5Q/69e8923506cf7_0.jpg', '69e8923506cf7_0.jpg', 1, 0, '2026-04-22 09:17:41'),
(24, 'vydtqBe4ua5c', 'uploads/vydtqBe4ua5c/69e892609bc35_0.jpg', '69e892609bc35_0.jpg', 1, 0, '2026-04-22 09:18:24'),
(25, 'ofzFGvQI015m', 'uploads/ofzFGvQI015m/69e8927a7be68_0.jpg', '69e8927a7be68_0.jpg', 1, 0, '2026-04-22 09:18:50'),
(26, '6J3mHFzRSU5c', 'uploads/6J3mHFzRSU5c/69e8928fdf77f_0.jpg', '69e8928fdf77f_0.jpg', 1, 0, '2026-04-22 09:19:11'),
(27, 'wg13C0H9piXx', 'uploads/wg13C0H9piXx/69e892a61e933_0.jpg', '69e892a61e933_0.jpg', 1, 0, '2026-04-22 09:19:34'),
(28, 'yXvGoNBnH8Im', 'uploads/yXvGoNBnH8Im/69e892b8178f7_0.jpg', '69e892b8178f7_0.jpg', 1, 0, '2026-04-22 09:19:52'),
(29, 'i0kmFKh2gaDf', 'uploads/i0kmFKh2gaDf/69e892f842fbb_0.jpg', '69e892f842fbb_0.jpg', 1, 0, '2026-04-22 09:20:56'),
(31, 'B5S6TKeu8dzN', 'uploads/B5S6TKeu8dzN/69e8937b09862_0.jpg', '69e8937b09862_0.jpg', 1, 0, '2026-04-22 09:23:07'),
(35, 'pm1K5MDdLRfr', 'uploads/pm1K5MDdLRfr/69e893de21c55_0.jpg', '69e893de21c55_0.jpg', 1, 0, '2026-04-22 09:24:46'),
(36, 'xgk0bKo6Q1J5', 'uploads/xgk0bKo6Q1J5/69e89423ab5a6_0.jpg', '69e89423ab5a6_0.jpg', 1, 0, '2026-04-22 09:25:55'),
(38, 'LaivpEm3TtG8', 'uploads/LaivpEm3TtG8/69e8943d89c94_0.jpg', '69e8943d89c94_0.jpg', 1, 0, '2026-04-22 09:26:21'),
(39, 'HNsPAYRQyBxn', 'uploads/HNsPAYRQyBxn/69e897b88babc_0.jpg', '69e897b88babc_0.jpg', 1, 0, '2026-04-22 09:41:12'),
(40, 'khSPmQLDuyaF', 'uploads/khSPmQLDuyaF/69e8983d85e85_0.jpg', '69e8983d85e85_0.jpg', 1, 0, '2026-04-22 09:43:25'),
(41, '3QhE2TsaADr6', 'uploads/3QhE2TsaADr6/69e89860e1918_0.jpg', '69e89860e1918_0.jpg', 1, 0, '2026-04-22 09:44:00'),
(42, 'w8ToCVufvsyW', 'uploads/w8ToCVufvsyW/69e8987644082_0.jpg', '69e8987644082_0.jpg', 1, 0, '2026-04-22 09:44:22'),
(43, 'SD6TbiH7VkgL', 'uploads/SD6TbiH7VkgL/69e898997431a_0.webp', '69e898997431a_0.webp', 1, 0, '2026-04-22 09:44:57'),
(45, 't3kYFhfJN95B', 'uploads/t3kYFhfJN95B/69e898b957213_0.jpg', '69e898b957213_0.jpg', 1, 0, '2026-04-22 09:45:29'),
(46, 'H16OBEtbn3Rs', 'uploads/H16OBEtbn3Rs/69e898e13b81a_0.jpg', '69e898e13b81a_0.jpg', 1, 0, '2026-04-22 09:46:09'),
(47, 'sW1ZUvcx3b7g', 'uploads/sW1ZUvcx3b7g/69e898fb4f875_0.jpg', '69e898fb4f875_0.jpg', 1, 0, '2026-04-22 09:46:35'),
(48, 'VaAbimox3Qtw', 'uploads/VaAbimox3Qtw/69e899580ed8d_0.jpg', '69e899580ed8d_0.jpg', 1, 0, '2026-04-22 09:48:08'),
(49, '0F9pInUbugyG', 'uploads/0F9pInUbugyG/69e8997ebde6c_0.jpg', '69e8997ebde6c_0.jpg', 1, 0, '2026-04-22 09:48:46'),
(50, 'jk4mJ8qwXasp', 'uploads/jk4mJ8qwXasp/69e899a30b4a5_0.jpg', '69e899a30b4a5_0.jpg', 1, 0, '2026-04-22 09:49:23'),
(52, 'eoghnOHJiYm3', 'uploads/eoghnOHJiYm3/69e899ff51672_0.jpg', '69e899ff51672_0.jpg', 1, 0, '2026-04-22 09:50:55'),
(54, 'qW6sAX8gpjKn', 'uploads/qW6sAX8gpjKn/69e89a6923c40_0.jpg', '69e89a6923c40_0.jpg', 1, 0, '2026-04-22 09:52:41'),
(55, 'uFIQw9NJERmS', 'uploads/uFIQw9NJERmS/69e89ad787c9a_0.jpg', '69e89ad787c9a_0.jpg', 1, 0, '2026-04-22 09:54:31'),
(57, 'xUTCa64wAtuR', 'uploads/xUTCa64wAtuR/69e89af751f3a_0.jpg', '69e89af751f3a_0.jpg', 1, 0, '2026-04-22 09:55:03'),
(60, 'zjVx6oQhCimd', 'uploads/zjVx6oQhCimd/69e89b62e504b_0.webp', '69e89b62e504b_0.webp', 1, 0, '2026-04-22 09:56:50'),
(61, 'Fe7wz6lJK13I', 'uploads/Fe7wz6lJK13I/69e89b9757fba_0.jpg', '69e89b9757fba_0.jpg', 1, 0, '2026-04-22 09:57:43'),
(64, 'urgcqztaFTiD', 'uploads/urgcqztaFTiD/69e89beb59fdf_0.jpg', '69e89beb59fdf_0.jpg', 1, 0, '2026-04-22 09:59:07'),
(65, '2AFxdybwULag', 'uploads/2AFxdybwULag/69e89c2d61dae_0.jpg', '69e89c2d61dae_0.jpg', 1, 0, '2026-04-22 10:00:13'),
(67, 'GdXme0bzr1Bv', 'uploads/GdXme0bzr1Bv/69e89cb17e69d_0.jpg', '69e89cb17e69d_0.jpg', 1, 0, '2026-04-22 10:02:25'),
(73, 'BmaUIHkpAl8b', 'uploads/BmaUIHkpAl8b/69e89eb324dcf_0.jpg', '69e89eb324dcf_0.jpg', 1, 0, '2026-04-22 10:10:59'),
(74, 'IJK87PGuqgsi', 'uploads/IJK87PGuqgsi/69e89ee82573e_0.webp', '69e89ee82573e_0.webp', 1, 0, '2026-04-22 10:11:52'),
(75, '58nZ049eKtsB', 'uploads/58nZ049eKtsB/69e89f09ac467_0.jpg', '69e89f09ac467_0.jpg', 1, 0, '2026-04-22 10:12:25'),
(76, '7OqUcxNwVbk2', 'uploads/7OqUcxNwVbk2/69e89f209e60d_0.jpg', '69e89f209e60d_0.jpg', 1, 0, '2026-04-22 10:12:48'),
(79, 'DQyRINire4qs', 'uploads/DQyRINire4qs/69e89f6a88d39_0.jpg', '69e89f6a88d39_0.jpg', 1, 0, '2026-04-22 10:14:02'),
(81, '4C5Ac78FQrEo', 'uploads/4C5Ac78FQrEo/69e89fcb2c025_0.jpg', '69e89fcb2c025_0.jpg', 1, 0, '2026-04-22 10:15:39');

-- --------------------------------------------------------

--
-- Tábla szerkezet ehhez a táblához `message_reports`
--

CREATE TABLE `message_reports` (
  `id` int(11) NOT NULL,
  `message_id` char(25) NOT NULL,
  `reporter_user_id` int(11) NOT NULL,
  `reason` text NOT NULL,
  `status` enum('pending','resolved','dismissed') DEFAULT 'pending',
  `created_at` timestamp NULL DEFAULT current_timestamp()
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- --------------------------------------------------------

--
-- Tábla szerkezet ehhez a táblához `orders`
--

CREATE TABLE `orders` (
  `id` char(12) NOT NULL,
  `buyer_id` int(11) NOT NULL,
  `seller_id` int(11) NOT NULL,
  `item_id` char(12) NOT NULL,
  `status` enum('pending','completed','cancelled') DEFAULT 'pending',
  `shipping_name` varchar(255) NOT NULL,
  `shipping_email` varchar(255) NOT NULL,
  `shipping_phone` varchar(50) NOT NULL,
  `shipping_zip` varchar(20) NOT NULL,
  `shipping_city` varchar(100) NOT NULL,
  `shipping_address` varchar(255) NOT NULL,
  `payment_method` enum('cod','transfer','pickup') NOT NULL,
  `notes` text DEFAULT NULL,
  `created_at` timestamp NULL DEFAULT current_timestamp()
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- --------------------------------------------------------

--
-- Tábla szerkezet ehhez a táblához `passwords`
--

CREATE TABLE `passwords` (
  `id` int(11) NOT NULL,
  `password_hash` varchar(255) NOT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

--
-- A tábla adatainak kiíratása `passwords`
--

INSERT INTO `passwords` (`id`, `password_hash`) VALUES
(3, '$2y$10$ADHEeoYMYkfH4MeHrj6GOOp8pbb7BWfLLHzTPicp8vGXQkewQpvBO'),
(7, '$2y$10$AkIZEKmqreqUBUgvkA6Ww.pYWIl.z5.2aBL8JhSFdlCh1JIrTfTt2'),
(2, '$2y$10$J89VvG7zz4lJY11BtfBgEeEVfbW05QqgAwa8GT3vkH7F1Ei3Pax6i'),
(5, '$2y$10$JgOKMx5DgGwhJ9v0G6bb.OH9l7nMOKd5fekM4g.WNHEIrJufHbjma'),
(4, '$2y$10$Y2f0Ifg1D0bvt0qlrbeqMu1f5/Smo1.kO1JXljBc8L0jr5oR8X/eG'),
(6, '$2y$10$zPPPHy3GjSmIigbZGaQPv..7.GKZFQrTsimbe6CRUsp.L83aNe81W'),
(9, '$2y$12$0dXppLergP1zuILFqc.XdO7lQR9amlX/DyIR3Jz/VIbJi.sAR02Ey'),
(20, '$2y$12$185J4Pbcbxs.N10ajUAse.7y3n/KobbYif7NcM5MTWyS8WPLWxddq'),
(28, '$2y$12$2f0/t7Fb7c8B3UOjvsRf7uWiSC7alhy9OYSMVUyYh0ei75Y5qVE3S'),
(12, '$2y$12$4D4827ST/gwPgYrdTrny5ui9SWUK36ry9SGqkEmAnId.bXeB1APQG'),
(35, '$2y$12$7HKCZhzzrTOr.lzPnQZvSuBePav1aPPtTg5cZzrx.ISwgTnSBWZTu'),
(16, '$2y$12$8sNmCreleSiR6JHGM3Kk0u.TWb1XgtbcDBdofoJaXdOJ7GT5u04eS'),
(25, '$2y$12$9nMFXc18ABCp4W.h9Mi41uAMU7IuVCH5Hq9Y3m2HB9oI1nocre35G'),
(37, '$2y$12$9O3KYq.prEwt72CJoBTireye2AwVZTBRIOMeApUMnsDFv3ez0ensa'),
(17, '$2y$12$fBi9SgWVW64M1AEFLGf8k.pc5zQP6o/Zk7Rfv/00QEPr2sBN1fopm'),
(33, '$2y$12$GKrKmuJGj/Z/LNNs7wPzdeJyE2TrKlYDizJzY73D8YfATzhuPrPti'),
(10, '$2y$12$h0tixxvpz81KbDg9/GqLCeNVg8tHTO9zgUo2AGIm1INEzJSLy4Pnm'),
(15, '$2y$12$H79RSMRbvEV8nBkFmG2Mo.GWyaQ2XPM85aR61f6ZCIO/gF5qzGxfq'),
(13, '$2y$12$HhWU5DU7xwHXgbAlBsyq9uLGROA0h2PR6cLe6PJNiWfmo15r5ZcZG'),
(36, '$2y$12$HUdA6dn5Z9uaBsb5SAQY6.Aa6FyMblgJJ.7VeaxJe8wur6MfM.idm'),
(18, '$2y$12$HyJHieqmLHEVUqhh7sXoUOOg5tRdWCp0p4vse4YVHjTE4q2KvVdJK'),
(29, '$2y$12$IASCQ30iUuHifl61KcLN6eZv63JtbuINOZg9/TaRJCSaG7Sk6Ygc6'),
(11, '$2y$12$nByGQGgw53zkxMdL5gCpT.v4ngFdGAu3H7WyYUSBQwn/NaLviSib.'),
(26, '$2y$12$oTSCvwe8QWwYKhKhLhXneekMbfFLJ7gW8PdPUJcgPSjJQ2Rcnbsz2'),
(8, '$2y$12$oVCXR//Pnux3eLFwazox2.YQlH4Tgy9dxmVsiMbXMik8B0I2cfv8i'),
(27, '$2y$12$PHGUhASXwLpxmIkP8NHUVutYAm1fDye1rBYXLl5xinbA0dVxPBG/C'),
(24, '$2y$12$qAgEZhnqu3zULqGvNs2pPuhEPmxxyr1pMG/A6hFs.gsAK6nFVr6IG'),
(23, '$2y$12$suuS0u4phZ5DVLnbbiXfQuZrxxf9wnLgsDjE.sfmb12Sv5VAzlABq'),
(1, '$2y$12$sVBkSVoMk8OPmQjrUz6Ol.pfXvGrdvHOlvVe8rzHfx5GC5/PHlJIa'),
(22, '$2y$12$u/kl/1Tw5PUg7kbbghNVF.Vm9jfLLWOufXPYA4LMKu5Jyn3a8aDam'),
(31, '$2y$12$U5xHJwtqWX9mS6yp66KofeQfyX9ExfMXmAY9p/ORjhodg597/05Ke'),
(30, '$2y$12$uAaVCEkJpuvLyTA9j0nzPuS6viaYo27y/kv92./KjWD/UzHs86QZi'),
(34, '$2y$12$wecZ9EIfrOvOLsrUpkkI1ectCj9Cqb69cBruUHyfwgaDkvx9bl1.C'),
(14, '$2y$12$WFUqYJiPerPP7kkDcU7/eeQ0fsygRmOOAhISIzO9vJnLvwCj/hQ8q'),
(21, '$2y$12$WZFBFwPNowS3Iosl9RhoUuB1QCCjPrzeVzXEwtfr8u5TIEmlQixZO'),
(19, '$2y$12$xJ3ySM/4ItSPTHzYUtZBiOqrFQtwW3HBtp7GcLUDV/Nbb4aMuoOtS'),
(32, '$2y$12$zZRR1kAa37XrOTAqeOFd0OV6sKOD9q/2mQtmBMzYZY/I9HOjqql7S');

-- --------------------------------------------------------

--
-- Tábla szerkezet ehhez a táblához `reports`
--

CREATE TABLE `reports` (
  `id` int(11) NOT NULL,
  `item_id` char(12) NOT NULL,
  `user_id` int(11) NOT NULL,
  `reason` text NOT NULL,
  `status` enum('pending','resolved','dismissed') DEFAULT 'pending',
  `created_at` timestamp NULL DEFAULT current_timestamp()
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

--
-- A tábla adatainak kiíratása `reports`
--

INSERT INTO `reports` (`id`, `item_id`, `user_id`, `reason`, `status`, `created_at`) VALUES
(1, 'wYQeKZVOsrgy', 1, 'nem eredeti', 'pending', '2026-04-16 07:55:51'),
(2, 'qPhW5H9VgC2r', 3, 'phejj', 'pending', '2026-04-22 08:15:51'),
(4, 'cxYD7ZTtSbif', 7, 'nem tetszik nekem ez az oltony!', 'pending', '2026-04-22 09:21:05');

-- --------------------------------------------------------

--
-- Tábla szerkezet ehhez a táblához `users`
--

CREATE TABLE `users` (
  `id` int(11) NOT NULL,
  `email` varchar(255) NOT NULL,
  `username` varchar(100) NOT NULL,
  `password_id` int(11) NOT NULL,
  `created_at` timestamp NULL DEFAULT current_timestamp(),
  `profile_picture` varchar(255) DEFAULT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

--
-- A tábla adatainak kiíratása `users`
--

INSERT INTO `users` (`id`, `email`, `username`, `password_id`, `created_at`, `profile_picture`) VALUES
(1, 'admin@example.com', 'admin', 35, '2026-04-22 07:51:46', 'uploads/profile/user_1_1776939528.png'),
(2, 'martin@example.com', 'martin', 2, '2026-04-16 07:41:54', NULL),
(3, 'cuci@cuci.phejj', 'cuci', 3, '2026-04-20 07:00:48', NULL),
(7, 'gabi@gabi.gabi', 'gabi', 7, '2026-04-20 07:35:00', NULL),
(34, 'test@teszt.teszt', 'teszt', 36, '2026-04-29 10:50:04', NULL),
(35, '123@1.2', '123', 37, '2026-05-12 10:35:02', NULL);

-- --------------------------------------------------------

--
-- Tábla szerkezet ehhez a táblához `uzenetek`
--

CREATE TABLE `uzenetek` (
  `id` char(25) NOT NULL,
  `sender_id` int(11) NOT NULL,
  `receiver_id` int(11) NOT NULL,
  `message` text NOT NULL,
  `sent_at` timestamp NULL DEFAULT current_timestamp(),
  `is_read` tinyint(1) DEFAULT 0
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

--
-- A tábla adatainak kiíratása `uzenetek`
--

INSERT INTO `uzenetek` (`id`, `sender_id`, `receiver_id`, `message`, `sent_at`, `is_read`) VALUES
('022sdXiiPgcZVKtkDpQbpjJqn', 1, 2, 'elromlott a pászuj', '2026-04-20 07:20:53', 1),
('0oLipCni5EulmtpWoB6Vr4VOt', 1, 2, 'najo', '2026-04-20 07:20:15', 1),
('0otv1qBTwy4z5wtpwnHlurhEO', 2, 1, 'HOHOOOO', '2026-04-20 07:19:11', 1),
('1610Jfnlt2xvOm72myYoPZC1a', 1, 2, 'f', '2026-04-17 10:28:50', 1),
('1SmirvrkYvtxqB5O8iBZm7i0S', 2, 1, 'na jo', '2026-04-20 07:20:31', 1),
('2BvBgxCvEZFkX5MUIwH4Pj0q2', 1, 2, 'afs', '2026-04-17 10:28:47', 1),
('2bWURAIl6imBf9v8i8NDEig1i', 1, 2, 'bazdmeg', '2026-04-20 07:20:11', 1),
('2OkfpeENDPM7i6jHld4Q1m1Vj', 1, 2, 'a cevi most pedig mukodik', '2026-04-20 07:20:58', 1),
('2relEGFJEmlSoTh2AekQtl57H', 1, 2, 'NAJO', '2026-04-20 07:20:32', 1),
('3m784oyLu0afwFkReIfOFJNJZ', 1, 2, 'ott talá srácok', '2026-04-20 07:21:16', 1),
('3tugXT81EpABcbRPEcPjijyak', 1, 2, 'zseni vagy geci', '2026-04-20 07:19:08', 1),
('45wTMf2OiK8RdBwU6tnX1sOoF', 1, 2, 'lacika simnk nelukl', '2026-04-17 06:11:52', 1),
('5iG09Gea8JnNsVXNjs89K9vF9', 1, 2, 'f', '2026-04-17 10:28:51', 1),
('6VDuTFhCFdrhso0lyFPbFetFI', 1, 2, 'sf', '2026-04-17 10:28:50', 1),
('7cPWvgQ8Nr0OMUHaxf6UcTK1b', 1, 2, 'karoj', '2026-04-17 06:03:47', 1),
('7wZB8ukaeP8wJTvqDbV4bwn3R', 1, 3, 'gyaa', '2026-04-22 10:11:23', 1),
('82gvjqdlfQy8kCfQ56vzdzU44', 1, 2, 'ezazgeci', '2026-04-20 07:19:12', 1),
('8FrtjNVzfOm75LI2l8R5qfJZ0', 2, 1, 'cuci adj nekem ragot karoj', '2026-04-20 07:20:22', 1),
('8jNJcRPAF9lzzqybJWG3l9WLH', 2, 1, 'nem te', '2026-04-20 07:20:13', 1),
('8mHmdihqt8hlahDa8OtAVCopi', 1, 7, 'tedd a xamppba te SUCKMASZTÁR', '2026-04-20 07:36:13', 1),
('8vML2iwtLp9o1AbSMuM6AYrfJ', 2, 1, 'PHEJJ', '2026-04-22 08:26:38', 1),
('92vlAa7Ay2ZYNVnnr6Hn2pkr4', 1, 2, 'af', '2026-04-17 10:28:49', 1),
('9iTmUPfyLVFb7ZmvCfPgZGI50', 1, 2, 'ritká', '2026-04-20 07:21:03', 1),
('9Kcwlq8GSAqlk3AF4yxS1Rop9', 2, 1, 'csááá KAROJ', '2026-04-20 07:20:09', 1),
('9u7586kGcoEw3TP8RDgTdkcLi', 1, 2, 'fsf', '2026-04-17 10:28:49', 1),
('akn8uGnbkwhsrhKmRUWfC8yRw', 1, 2, 'glfd', '2026-04-16 08:14:46', 1),
('AmPFAjmRXI7PwTJf1I8uctX8m', 1, 2, 'aha', '2026-04-16 08:14:40', 1),
('AQ5Pp5CbBEpai4lze27J1hB7M', 1, 2, 'fsa', '2026-04-17 10:28:46', 1),
('Avfg4ygMW4MMHltd2zj8wu1gS', 1, 2, 'KAROJ ', '2026-04-29 08:37:30', 1),
('AVjIRNm1sEutoEOypqkoPYoTY', 1, 2, 'fs', '2026-04-17 10:28:46', 1),
('BTpwcT4KuKtZGwjU3IPNcQpoN', 1, 2, 'fas', '2026-04-17 10:28:46', 1),
('C3fYjct2b4kUfNXsHkpDDRN3j', 1, 2, 'PHEJJSUMMEG A KAROJ GYAAAAAAAAAAAAAAAAAAA', '2026-04-20 07:19:19', 1),
('ci6befNlVagcwoTVvfogXNVYG', 1, 2, 'szia', '2026-04-29 08:01:08', 1),
('CIZ6sxeEvJu6tZS4RHRSZNIEi', 1, 2, 'f', '2026-04-17 10:28:49', 1),
('cQV32sCXB9a56x4Qgz0AdUVLn', 1, 2, 'a cevi az fosphejj agy a martinnakragot', '2026-04-20 07:20:44', 1),
('d1Ujyyd1uflHsJ96qNdlvqgrV', 1, 2, 'SÜMMEG', '2026-04-29 08:37:31', 1),
('dhe0BihpzUIHcTfJjUkIqi0Xl', 7, 1, 'seggel felém alszol', '2026-04-20 07:36:04', 1),
('dK3LqbpCY6YYdvVg4yg2FqY3o', 1, 2, 'f', '2026-04-17 10:28:54', 1),
('dUkGyexVuFkvZHnkclRwtOLxc', 1, 7, 'csa gabbi', '2026-04-20 07:35:44', 1),
('E4LBFUChyXcSV5CWXdxZVNLLc', 2, 1, 'phejj', '2026-04-16 08:26:54', 1),
('EdRYqr3Zl1YbqeD9WxfKKVBg6', 1, 2, 's', '2026-04-17 10:28:49', 1),
('eqS32woavqVEAvCo2PjKelsWU', 1, 2, 'fas', '2026-04-17 10:28:46', 1),
('fGpmNPMg8n2YJQ1u3nf3zZPHJ', 1, 2, 'a', '2026-04-17 10:28:50', 1),
('FjSM1kY51JoBqRMno2lMn1dVT', 1, 2, 'a', '2026-04-17 10:28:49', 1),
('FNAZeRQhHLnHOxiI97dabyXCS', 1, 2, 'asdfifsaff', '2026-04-17 10:28:45', 1),
('fpGsvmdel5jotI7Jbs7ZSMUl3', 3, 1, 'szia', '2026-04-22 10:11:09', 1),
('Fs5zjFRlWV9ayPYpqlgInjLv4', 3, 2, 'Nagyon fasza phejj', '2026-04-24 10:45:05', 1),
('fsZeJuKOGk3mIsOyoQTGiU1Az', 1, 2, 'fasf', '2026-04-17 10:28:48', 1),
('GkYBiZp79OW4LlfJLEPtYu9Q1', 1, 3, 'phejj', '2026-04-22 10:11:19', 1),
('gmrrzC3h8jv8ufah3t0WDq8tG', 1, 2, 'summeg', '2026-04-22 08:26:32', 1),
('GNmMNiuN0OHTL9tLA6gvWnPnZ', 1, 2, 'asoőkfsaofősaoő', '2026-04-17 10:28:47', 1),
('HczqcoLt0Tf1F2hOs0JfQi3Gb', 1, 2, 'csá gecccccccccccccccccccciiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiiii', '2026-04-20 07:19:56', 1),
('hlpDltFpyR6NtaRlhRBdEoOwp', 2, 1, 'hjk', '2026-04-16 08:14:44', 1),
('iO47MI77MA4rhUw0e6IpDHom0', 1, 2, 'a', '2026-04-17 10:28:50', 1),
('iPuElbwgGXBYvPq5rkqRYfBID', 2, 1, 'csa cuci', '2026-04-20 07:18:22', 1),
('Isc9JVkKH6Fia1RcIF7biioIo', 2, 1, 'PHEJJ', '2026-04-20 07:21:06', 1),
('JcGPp8Y4rdpHi2EbYjUiKe1do', 1, 2, 's', '2026-04-17 10:28:50', 1),
('jfMy2Oz3ADKYE4pXMMSVTSeqo', 7, 1, 'gyere', '2026-04-20 07:35:57', 1),
('JHWriKBWAYd33cdvCyqudBZNt', 1, 2, 'karoj', '2026-04-22 08:26:41', 1),
('JJLKLm5EBrEAAou5Z4Oc6MSzU', 1, 2, 'fsa', '2026-04-17 10:28:46', 1),
('KEq3OeHf7Jn8ieINlWAgtV6Qt', 2, 1, 'Megyünk a gabbival börger kingbe kaposba', '2026-04-20 07:20:56', 1),
('KFOy4Isbuqgd8VLRlnOjx7Fjc', 2, 1, 'sapnu puas', '2026-04-22 08:26:28', 1),
('KfRstamsi9cQMEUpdJQKeN3ZB', 2, 1, 'BASZOD KAROJ', '2026-04-20 07:19:04', 1),
('KIYDzKDPuhN2MG1caTT8lvGmM', 2, 1, 'szia', '2026-04-16 08:14:29', 1),
('l4Ht6MM4ygmqGeGQTl8bmeC35', 1, 7, 'csá gabbi', '2026-04-24 10:14:18', 1),
('LgWK3MObeSKdTTaru5tebXG1Y', 1, 2, 'fs', '2026-04-17 10:28:46', 1),
('LS6PpO0P61g5j6X6em3OY0RFh', 2, 1, 'PHEJJAHHH', '2026-04-20 07:19:13', 1),
('lY0Zfgcw7n9qa6im3hGpIzRca', 1, 2, 'tudom', '2026-04-20 07:21:04', 1),
('msDQlYgx9OI8dCCuQBHB9hEli', 1, 2, 'GYAAAAAAA NINCSEN', '2026-04-20 07:20:30', 1),
('ngqq075XYRjjw879AJU9J1LoJ', 7, 1, 'phejjjjjjjjjjjaaaaaaaaaaaaa', '2026-04-20 07:35:40', 1),
('OCdwRVahVKPvvkpwsws9Mx80A', 7, 1, 'bazdmeganyad', '2026-04-20 07:35:48', 1),
('ogO88nHhCKJDUjSUyQJTU4eV1', 1, 2, 'sf', '2026-04-17 10:28:51', 1),
('oNp9kmaDg7ipgNQmUpwRTUIvX', 1, 3, 'fasz', '2026-04-22 10:11:18', 1),
('pDHRjhH9s2FV9QN07l1NwCE0C', 1, 2, 'fw', '2026-04-17 10:28:50', 1),
('pUS4VJ3g6t1T3ToKb7i7yHFxK', 1, 7, 'laci add a segged karoj', '2026-04-20 07:35:52', 1),
('Qds1cgIzMMAJuWrdctiXouywo', 1, 7, 'bumzi vagy', '2026-04-20 07:35:46', 1),
('QhCrokQ6tGzUDJplAqLOd0Dpn', 1, 2, 'a', '2026-04-17 10:28:51', 1),
('QWSnKLHBqN8LzSe9L9vpanIBH', 1, 2, 'gdfgf', '2026-04-16 08:14:48', 1),
('QxbQpCW6nae0v9PBZlpEIyFqz', 2, 1, 'PAKK', '2026-04-20 07:20:36', 1),
('r4MuVqKKWzn7jQMwxdjjYxZ4g', 1, 2, 'afs', '2026-04-17 10:28:45', 1),
('RDyCgLWKStw57FqMc3vpWvorl', 1, 2, 'fs', '2026-04-17 10:28:46', 1),
('rL2KBooIj86Vd0owzmxhDT9ER', 2, 1, 'aha', '2026-04-29 10:37:28', 1),
('rm3X8czRkU4u48n8EnGYMhYfF', 1, 3, 'sümmeg', '2026-04-22 10:11:22', 1),
('SaDUxPLZcWKmTx14BT4a47qHs', 1, 2, 'sfa', '2026-04-17 10:28:45', 1),
('SbTqDk2mjEWZnGpkSxdKUuFRK', 1, 2, 'phejj', '2026-04-20 07:19:00', 1),
('sjcZjIp36PAdwPkpg61hWiODS', 1, 2, 'asfas', '2026-04-17 10:28:45', 1),
('SQyMS4LnyhVTmNZYpG7gE9DfV', 1, 7, 'phejj', '2026-04-27 11:59:55', 1),
('t9saOrf1uA9v0AcNlKjNSKn6m', 1, 2, 'fas', '2026-04-17 10:28:46', 1),
('TB0zz2LTUleVRLb2XDYkhf0iA', 1, 2, 'cuci adja martinnak ragotkarojj', '2026-04-20 07:20:27', 1),
('TINk9Ko87OpUZx01QSckuqqgj', 1, 2, 'anusz banusz', '2026-04-17 06:11:41', 1),
('TNoXzuQkGcLiit1Fzg5aGwDcX', 2, 3, 'PHEJJ', '2026-04-24 10:56:06', 1),
('TsonVvC7WOp5RKyxS6dD4mN2e', 1, 2, 'phejj karoj sümmeg gyaa', '2026-04-17 06:11:57', 1),
('um2SP6xtTnW7F98bVZulMpBSd', 1, 2, 'GYAA', '2026-04-29 08:37:32', 1),
('uuQi2IK1H8yxruuhh7i9x6Vvc', 1, 2, 'faskfkafsokasfka', '2026-04-17 10:28:48', 1),
('V1wuRfnB99Aj6rQ3JpK41rqpi', 1, 2, 'afs', '2026-04-17 10:28:45', 1),
('vaSfs9gnZUXRAQofMPs1JNI2l', 1, 2, 'sf', '2026-04-17 10:28:47', 1),
('vgFQ6dqjxwB0aul1OHTyDqaBP', 2, 1, 'ott talá srácok', '2026-04-20 07:20:58', 1),
('vis82TfKWObCFr1Y3EflD1Qod', 1, 2, 'a', '2026-04-17 10:28:49', 1),
('vLZdoQxZULNgSCSPgn0FF1ZDN', 1, 2, 'a', '2026-04-17 10:28:50', 1),
('VSerFM0dxG6E81oXctQkAyzmC', 1, 2, 'sümmeg', '2026-04-17 06:03:58', 1),
('wxyukcGZWNqXVQByhlxDb3C8T', 1, 2, 'saf', '2026-04-17 10:28:46', 1),
('XfizcxogbWglBA2WX74DjyIFr', 2, 1, 'gyaaa nincsne', '2026-04-20 07:20:30', 1),
('XnL5QIbgnWGIwYwruzHkdtvfS', 1, 2, 'PHEJJ', '2026-04-29 08:37:29', 1),
('YF7Gqp3LjjTrDgrSzmleFZbr2', 1, 7, 'Csá Gabbi', '2026-04-22 09:44:26', 1),
('yJcUZOgi5W1jri9KYDZihqXKs', 1, 2, 'as', '2026-04-17 10:28:48', 1),
('zR4oZK9lQSVMO4ZHt6KoD5XyA', 1, 2, 's', '2026-04-20 07:19:02', 1),
('ZweJ8rXhP2ewFYZDj8FxGX2yg', 1, 3, 'karoj', '2026-04-22 10:11:20', 1);

-- --------------------------------------------------------

--
-- Tábla szerkezet ehhez a táblához `vizsgalock_exceptions`
--

CREATE TABLE `vizsgalock_exceptions` (
  `user_id` int(11) NOT NULL,
  `added_at` timestamp NULL DEFAULT current_timestamp()
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

--
-- A tábla adatainak kiíratása `vizsgalock_exceptions`
--

INSERT INTO `vizsgalock_exceptions` (`user_id`, `added_at`) VALUES
(34, '2026-04-29 10:56:59');

-- --------------------------------------------------------

--
-- Tábla szerkezet ehhez a táblához `vizsgalock_settings`
--

CREATE TABLE `vizsgalock_settings` (
  `id` int(11) NOT NULL DEFAULT 1,
  `is_locked` tinyint(1) DEFAULT 0,
  `updated_at` timestamp NULL DEFAULT current_timestamp() ON UPDATE current_timestamp()
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

--
-- A tábla adatainak kiíratása `vizsgalock_settings`
--

INSERT INTO `vizsgalock_settings` (`id`, `is_locked`, `updated_at`) VALUES
(1, 0, '2026-05-05 12:09:12');

--
-- Indexek a kiírt táblákhoz
--

--
-- A tábla indexei `admins`
--
ALTER TABLE `admins`
  ADD PRIMARY KEY (`user_id`);

--
-- A tábla indexei `hidden_conversations`
--
ALTER TABLE `hidden_conversations`
  ADD PRIMARY KEY (`user_id`,`partner_id`),
  ADD KEY `fk_hc_partner` (`partner_id`);

--
-- A tábla indexei `items`
--
ALTER TABLE `items`
  ADD PRIMARY KEY (`id`),
  ADD KEY `fk_items_users` (`user_id`);

--
-- A tábla indexei `item_images`
--
ALTER TABLE `item_images`
  ADD PRIMARY KEY (`id`),
  ADD KEY `fk_images_items` (`item_id`);

--
-- A tábla indexei `message_reports`
--
ALTER TABLE `message_reports`
  ADD PRIMARY KEY (`id`),
  ADD KEY `message_id` (`message_id`),
  ADD KEY `reporter_user_id` (`reporter_user_id`);

--
-- A tábla indexei `orders`
--
ALTER TABLE `orders`
  ADD PRIMARY KEY (`id`),
  ADD KEY `buyer_id` (`buyer_id`),
  ADD KEY `seller_id` (`seller_id`),
  ADD KEY `item_id` (`item_id`);

--
-- A tábla indexei `passwords`
--
ALTER TABLE `passwords`
  ADD PRIMARY KEY (`id`),
  ADD UNIQUE KEY `uniq_password_hash` (`password_hash`);

--
-- A tábla indexei `reports`
--
ALTER TABLE `reports`
  ADD PRIMARY KEY (`id`),
  ADD KEY `item_id` (`item_id`),
  ADD KEY `user_id` (`user_id`);

--
-- A tábla indexei `users`
--
ALTER TABLE `users`
  ADD PRIMARY KEY (`id`),
  ADD UNIQUE KEY `uniq_email` (`email`),
  ADD UNIQUE KEY `uniq_username` (`username`),
  ADD KEY `fk_users_passwords` (`password_id`);

--
-- A tábla indexei `uzenetek`
--
ALTER TABLE `uzenetek`
  ADD PRIMARY KEY (`id`),
  ADD KEY `idx_sender` (`sender_id`),
  ADD KEY `idx_receiver` (`receiver_id`),
  ADD KEY `idx_sent_at` (`sent_at`);

--
-- A tábla indexei `vizsgalock_exceptions`
--
ALTER TABLE `vizsgalock_exceptions`
  ADD PRIMARY KEY (`user_id`);

--
-- A tábla indexei `vizsgalock_settings`
--
ALTER TABLE `vizsgalock_settings`
  ADD PRIMARY KEY (`id`);

--
-- A kiírt táblák AUTO_INCREMENT értéke
--

--
-- AUTO_INCREMENT a táblához `item_images`
--
ALTER TABLE `item_images`
  MODIFY `id` int(11) NOT NULL AUTO_INCREMENT, AUTO_INCREMENT=102;

--
-- AUTO_INCREMENT a táblához `message_reports`
--
ALTER TABLE `message_reports`
  MODIFY `id` int(11) NOT NULL AUTO_INCREMENT, AUTO_INCREMENT=3;

--
-- AUTO_INCREMENT a táblához `passwords`
--
ALTER TABLE `passwords`
  MODIFY `id` int(11) NOT NULL AUTO_INCREMENT, AUTO_INCREMENT=38;

--
-- AUTO_INCREMENT a táblához `reports`
--
ALTER TABLE `reports`
  MODIFY `id` int(11) NOT NULL AUTO_INCREMENT, AUTO_INCREMENT=12;

--
-- AUTO_INCREMENT a táblához `users`
--
ALTER TABLE `users`
  MODIFY `id` int(11) NOT NULL AUTO_INCREMENT, AUTO_INCREMENT=36;

--
-- Megkötések a kiírt táblákhoz
--

--
-- Megkötések a táblához `admins`
--
ALTER TABLE `admins`
  ADD CONSTRAINT `fk_admins_users` FOREIGN KEY (`user_id`) REFERENCES `users` (`id`) ON DELETE CASCADE;

--
-- Megkötések a táblához `hidden_conversations`
--
ALTER TABLE `hidden_conversations`
  ADD CONSTRAINT `fk_hc_partner` FOREIGN KEY (`partner_id`) REFERENCES `users` (`id`) ON DELETE CASCADE,
  ADD CONSTRAINT `fk_hc_user` FOREIGN KEY (`user_id`) REFERENCES `users` (`id`) ON DELETE CASCADE;

--
-- Megkötések a táblához `items`
--
ALTER TABLE `items`
  ADD CONSTRAINT `fk_items_users` FOREIGN KEY (`user_id`) REFERENCES `users` (`id`) ON DELETE CASCADE;

--
-- Megkötések a táblához `item_images`
--
ALTER TABLE `item_images`
  ADD CONSTRAINT `fk_images_items` FOREIGN KEY (`item_id`) REFERENCES `items` (`id`) ON DELETE CASCADE;

--
-- Megkötések a táblához `message_reports`
--
ALTER TABLE `message_reports`
  ADD CONSTRAINT `message_reports_ibfk_1` FOREIGN KEY (`message_id`) REFERENCES `uzenetek` (`id`) ON DELETE CASCADE,
  ADD CONSTRAINT `message_reports_ibfk_2` FOREIGN KEY (`reporter_user_id`) REFERENCES `users` (`id`) ON DELETE CASCADE;

--
-- Megkötések a táblához `orders`
--
ALTER TABLE `orders`
  ADD CONSTRAINT `orders_ibfk_1` FOREIGN KEY (`buyer_id`) REFERENCES `users` (`id`) ON DELETE CASCADE,
  ADD CONSTRAINT `orders_ibfk_2` FOREIGN KEY (`seller_id`) REFERENCES `users` (`id`) ON DELETE CASCADE,
  ADD CONSTRAINT `orders_ibfk_3` FOREIGN KEY (`item_id`) REFERENCES `items` (`id`) ON DELETE CASCADE;

--
-- Megkötések a táblához `reports`
--
ALTER TABLE `reports`
  ADD CONSTRAINT `reports_ibfk_1` FOREIGN KEY (`item_id`) REFERENCES `items` (`id`) ON DELETE CASCADE,
  ADD CONSTRAINT `reports_ibfk_2` FOREIGN KEY (`user_id`) REFERENCES `users` (`id`) ON DELETE CASCADE;

--
-- Megkötések a táblához `users`
--
ALTER TABLE `users`
  ADD CONSTRAINT `fk_users_passwords` FOREIGN KEY (`password_id`) REFERENCES `passwords` (`id`);

--
-- Megkötések a táblához `uzenetek`
--
ALTER TABLE `uzenetek`
  ADD CONSTRAINT `fk_uzenetek_receiver` FOREIGN KEY (`receiver_id`) REFERENCES `users` (`id`) ON DELETE CASCADE,
  ADD CONSTRAINT `fk_uzenetek_sender` FOREIGN KEY (`sender_id`) REFERENCES `users` (`id`) ON DELETE CASCADE;

--
-- Megkötések a táblához `vizsgalock_exceptions`
--
ALTER TABLE `vizsgalock_exceptions`
  ADD CONSTRAINT `fk_vl_exceptions_users` FOREIGN KEY (`user_id`) REFERENCES `users` (`id`) ON DELETE CASCADE;
COMMIT;

/*!40101 SET CHARACTER_SET_CLIENT=@OLD_CHARACTER_SET_CLIENT */;
/*!40101 SET CHARACTER_SET_RESULTS=@OLD_CHARACTER_SET_RESULTS */;
/*!40101 SET COLLATION_CONNECTION=@OLD_COLLATION_CONNECTION */;
