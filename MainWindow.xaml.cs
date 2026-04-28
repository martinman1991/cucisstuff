using MySql.Data.MySqlClient;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace cucisstuff
{
    public partial class MainWindow : Window
    {
        // Adatbázis kapcsolat adatai a config.php-ból
        private const string DB_HOST = "localhost";
        private const string DB_USER = "cuci_ady_pepa_hu_usr";
        private const string DB_PASS = "utQTkTN2Q5WD7zal8IBFmQ";
        private const string DB_NAME = "cuci_ady_pepa_hu";

        private string ConnectionString =>
            $"Server={DB_HOST};Database={DB_NAME};Uid={DB_USER};Pwd={DB_PASS};Charset=utf8mb4;";

        // Session-szerű adatok
        public static int? LoggedInUserId { get; set; }
        public static string LoggedInUsername { get; set; }
        public static bool IsAdmin { get; set; }

        private static readonly SolidColorBrush PlaceholderBrush =
            new SolidColorBrush(Color.FromRgb(0x8A, 0x7A, 0x65));
        private static readonly SolidColorBrush NormalTextBrush =
            new SolidColorBrush(Color.FromRgb(0xF5, 0xF0, 0xE8));

        public MainWindow()
        {
            InitializeComponent();
            MainFrame.Navigate(new LoginPage(this));
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 1)
                DragMove();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        // ============================================
        // NAVIGÁCIÓS SEGÉDFÜGGVÉNYEK
        // ============================================
        public void NavigateToLogin()
        {
            LoggedInUserId = null;
            LoggedInUsername = null;
            IsAdmin = false;
            MainFrame.Navigate(new LoginPage(this));
        }

        public void NavigateToMain()
        {
            MainFrame.Navigate(new MainPage(this));
        }

        public void NavigateToAccount()
        {
            MainFrame.Navigate(new AccountPage(this));
        }

        public void NavigateToAdmin()
        {
            MainFrame.Navigate(new AdminPage(this));
        }

        public void NavigateToUpload()
        {
            MainFrame.Navigate(new UploadPage(this));
        }

        public void NavigateToMessages()
        {
            MainFrame.Navigate(new MessagesPage(this));
        }

        public void NavigateToPurchase(string itemId)
        {
            MainFrame.Navigate(new PurchasePage(this, itemId));
        }

        // ============================================
        // ADATBÁZIS SEGÉDFÜGGVÉNYEK
        // ============================================
        public MySqlConnection GetConnection()
        {
            var conn = new MySqlConnection(ConnectionString);
            conn.Open();
            return conn;
        }

        public bool CheckVizsgalock()
        {
            try
            {
                using (var conn = GetConnection())
                using (var cmd = new MySqlCommand(
                    "SELECT is_locked FROM vizsgalock_settings WHERE id = 1", conn))
                {
                    var result = cmd.ExecuteScalar();
                    return result != null && Convert.ToBoolean(result);
                }
            }
            catch
            {
                return false;
            }
        }

        public bool IsVizsgalockExcepted(int userId)
        {
            try
            {
                using (var conn = GetConnection())
                {
                    // Adminok automatikusan kivételek
                    using (var cmd = new MySqlCommand(
                        "SELECT COUNT(*) FROM admins WHERE user_id = @uid", conn))
                    {
                        cmd.Parameters.AddWithValue("@uid", userId);
                        if (Convert.ToInt32(cmd.ExecuteScalar()) > 0)
                            return true;
                    }
                    // Explicit kivételek
                    using (var cmd = new MySqlCommand(
                        "SELECT COUNT(*) FROM vizsgalock_exceptions WHERE user_id = @uid", conn))
                    {
                        cmd.Parameters.AddWithValue("@uid", userId);
                        if (Convert.ToInt32(cmd.ExecuteScalar()) > 0)
                            return true;
                    }
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        public bool CanPerformWriteOperation()
        {
            if (!CheckVizsgalock()) return true;
            if (LoggedInUserId.HasValue && IsVizsgalockExcepted(LoggedInUserId.Value)) return true;
            return false;
        }

        public string GenerateId(int length = 12)
        {
            const string chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var random = new Random();
            return new string(Enumerable.Range(0, length)
                .Select(_ => chars[random.Next(chars.Length)]).ToArray());
        }

        // ============================================
        // JELSZÓ HASH ELŐÁLLÍTÁS (BCrypt)
        // ============================================
        public string BCryptHash(string password)
        {
            return BCrypt.Net.BCrypt.HashPassword(password, BCrypt.Net.BCrypt.GenerateSalt(12));
        }

        public bool BCryptVerify(string password, string hash)
        {
            try
            {
                return BCrypt.Net.BCrypt.Verify(password, hash);
            }
            catch
            {
                return false;
            }
        }

        // ============================================
        // KÉP ÁTMÉRETEZŐ FÜGGVÉNY (WPF verzió)
        // ============================================
        public byte[] ResizeImage(byte[] imageData, int maxDim = 1024)
        {
            try
            {
                using (var ms = new MemoryStream(imageData))
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.StreamSource = ms;
                    bitmap.EndInit();

                    int srcWidth = bitmap.PixelWidth;
                    int srcHeight = bitmap.PixelHeight;

                    if (srcWidth <= maxDim && srcHeight <= maxDim)
                        return imageData;

                    double ratio = (double)srcWidth / srcHeight;
                    int newWidth, newHeight;
                    if (srcWidth > srcHeight)
                    {
                        newWidth = maxDim;
                        newHeight = (int)Math.Round(maxDim / ratio);
                    }
                    else
                    {
                        newHeight = maxDim;
                        newWidth = (int)Math.Round(maxDim * ratio);
                    }

                    var resizedBitmap = new TransformedBitmap(bitmap, new ScaleTransform(
                        (double)newWidth / srcWidth, (double)newHeight / srcHeight));

                    var encoder = new JpegBitmapEncoder { QualityLevel = 85 };
                    encoder.Frames.Add(BitmapFrame.Create(resizedBitmap));

                    using (var outputMs = new MemoryStream())
                    {
                        encoder.Save(outputMs);
                        return outputMs.ToArray();
                    }
                }
            }
            catch
            {
                return imageData;
            }
        }
    }

    // ============================================
    // VIEWMODEL ALAPOSZTÁLY
    // ============================================
    public class ViewModelBase : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    // ============================================
    // ITEM VIEWMODEL
    // ============================================
    public class ItemViewModel : ViewModelBase
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public decimal Price { get; set; }
        public string PriceFormatted => $"{Price:N0} Ft";
        public string SellerName { get; set; }
        public int SellerId { get; set; }
        public string Description { get; set; }
        public DateTime CreatedAt { get; set; }
        public string CreatedAtFormatted => CreatedAt.ToString("yyyy-MM-dd");
        public bool IsSold { get; set; }
        public string FirstImagePath { get; set; }
        public List<string> AllImagePaths { get; set; } = new List<string>();
        public int ImageCount => AllImagePaths?.Count ?? 0;

        public string StatusText => IsSold ? "🔴 ELKELT" : "🟢 AKTÍV";
        public Brush StatusColor
        {
            get
            {
                if (IsSold)
                    return new SolidColorBrush(Color.FromRgb(0xFF, 0x44, 0x44));
                else
                    return new SolidColorBrush(Color.FromRgb(0x00, 0xC8, 0x51));
            }
        }
    }

    // ============================================
    // USER VIEWMODEL (admin táblázatokhoz)
    // ============================================
    public class UserViewModel : ViewModelBase
    {
        public int Id { get; set; }
        public string Username { get; set; }
        public string Email { get; set; }
        public bool IsAdmin { get; set; }
        public int ItemCount { get; set; }
        public DateTime CreatedAt { get; set; }
        public string CreatedAtFormatted => CreatedAt.ToString("yyyy-MM-dd");
        public string RoleText => IsAdmin ? "■ ADMIN" : "○ USER";
    }

    // ============================================
    // ORDER VIEWMODEL
    // ============================================
    public class OrderViewModel : ViewModelBase
    {
        public string Id { get; set; }
        public string ItemTitle { get; set; }
        public string ItemId { get; set; }
        public string BuyerName { get; set; }
        public int BuyerId { get; set; }
        public string SellerName { get; set; }
        public int SellerId { get; set; }
        public decimal ItemPrice { get; set; }
        public string PriceFormatted => $"{ItemPrice:N0} Ft";
        public string Status { get; set; }
        public string PaymentMethod { get; set; }
        public string ShippingName { get; set; }
        public string ShippingEmail { get; set; }
        public string ShippingPhone { get; set; }
        public string ShippingZip { get; set; }
        public string ShippingCity { get; set; }
        public string ShippingAddress { get; set; }
        public string Notes { get; set; }
        public DateTime CreatedAt { get; set; }
        public string CreatedAtFormatted => CreatedAt.ToString("yyyy-MM-dd");
        public bool IsDetailsExpanded { get; set; }
    }

    // ============================================
    // MESSAGE / PARTNER VIEWMODEL
    // ============================================
    public class PartnerViewModel : ViewModelBase
    {
        public int Id { get; set; }
        public string Username { get; set; }
        public string ProfilePicture { get; set; }
        public string AvatarInitial
        {
            get
            {
                if (string.IsNullOrEmpty(Username))
                    return "?";
                else
                    return Username[0].ToString().ToUpper();
            }
        }
        public int UnreadCount { get; set; }
        public bool HasUnread => UnreadCount > 0;
        public DateTime LastMessageAt { get; set; }
        public string LastMessageTimeFormatted
        {
            get
            {
                var diff = DateTime.Now - LastMessageAt;
                if (diff.TotalMinutes < 1) return "Az imént";
                if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes} perce";
                if (diff.TotalHours < 24) return $"{(int)diff.TotalHours} órája";
                return LastMessageAt.ToString("yyyy.MM.dd");
            }
        }
    }

    public class MessageViewModel : ViewModelBase
    {
        public string Id { get; set; }
        public int SenderId { get; set; }
        public int ReceiverId { get; set; }
        public string Text { get; set; }
        public DateTime SentAt { get; set; }
        public string SentAtFormatted => SentAt.ToString("HH:mm");
        public bool IsRead { get; set; }
        public bool IsOwn { get; set; }
        public string Alignment => IsOwn ? "Right" : "Left";
        public Brush BubbleBackground
        {
            get
            {
                if (IsOwn)
                    return new LinearGradientBrush(
                        Color.FromRgb(0xFF, 0x8C, 0x00),
                        Color.FromRgb(0xC8, 0x50, 0x00), 45);
                else
                    return new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E));
            }
        }
        public Brush BubbleForeground
        {
            get
            {
                if (IsOwn)
                    return new SolidColorBrush(Colors.White);
                else
                    return new SolidColorBrush(Color.FromRgb(0xF5, 0xF0, 0xE8));
            }
        }
    }

    // ============================================
    // REPORT VIEWMODEL
    // ============================================
    public class ReportViewModel : ViewModelBase
    {
        public int Id { get; set; }
        public string ReportType { get; set; }
        public string RefId { get; set; }
        public string RefTitle { get; set; }
        public string ReporterName { get; set; }
        public int ReporterId { get; set; }
        public string TargetName { get; set; }
        public int TargetId { get; set; }
        public string Reason { get; set; }
        public string Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public string CreatedAtFormatted => CreatedAt.ToString("yyyy-MM-dd");
        public string TypeIcon => ReportType == "item" ? "◧ TERMÉK" : "✉ ÜZENET";
    }
}

// ============================================
// BEJELENTKEZÉS / REGISZTRÁCIÓ OLDAL
// ============================================
namespace cucisstuff
{
    public class LoginPage : Page
    {
        private readonly MainWindow _mainWindow;
        private TextBox LoginUsername;
        private PasswordBox LoginPassword;
        private TextBox RegUsername;
        private TextBox RegEmail;
        private PasswordBox RegPassword;
        private PasswordBox RegPassword2;
        private StackPanel LoginPanel;
        private StackPanel RegisterPanel;
        private TextBlock SubtitleText;
        private Border ErrorBorder;
        private TextBlock ErrorText;

        public LoginPage(MainWindow mainWindow)
        {
            _mainWindow = mainWindow;
            InitializeUI();
        }

        private void InitializeUI()
        {
            Background = new SolidColorBrush(Color.FromRgb(0x05, 0x05, 0x05));

            var outerGrid = new Grid();
            outerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            outerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(420) });
            outerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            outerGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            outerGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            outerGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var card = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x0F, 0x0F, 0x0F)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x29, 0xFF, 0x8C, 0x00)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(20),
                Padding = new Thickness(40, 36, 40, 36)
            };
            Grid.SetColumn(card, 1);
            Grid.SetRow(card, 1);

            var mainStack = new StackPanel();
            card.Child = mainStack;

            // Cím
            mainStack.Children.Add(new TextBlock
            {
                Text = "Cuci's Stuff",
                FontSize = 28,
                FontWeight = FontWeights.Light,
                Foreground = new SolidColorBrush(Color.FromRgb(0xF5, 0xF0, 0xE8)),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 6)
            });

            SubtitleText = new TextBlock
            {
                Text = "Bejelentkezés",
                FontSize = 14,
                Foreground = new SolidColorBrush(Color.FromRgb(0x8A, 0x7A, 0x65)),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 28)
            };
            mainStack.Children.Add(SubtitleText);

            // Hibaüzenet
            ErrorBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0x26, 0xFF, 0x32, 0x32)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x4D, 0xFF, 0x4D, 0x4D)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(12, 10, 12, 10),
                Margin = new Thickness(0, 0, 0, 16),
                Visibility = Visibility.Collapsed
            };
            ErrorText = new TextBlock
            {
                Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x80, 0x80)),
                FontSize = 13,
                TextWrapping = TextWrapping.Wrap,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            ErrorBorder.Child = ErrorText;
            mainStack.Children.Add(ErrorBorder);

            // ---- Login Panel ----
            LoginPanel = new StackPanel { Visibility = Visibility.Visible };

            LoginUsername = CreateInput("Felhasználónév vagy email");
            LoginPanel.Children.Add(LoginUsername);
            LoginPanel.Children.Add(new FrameworkElement { Height = 12 });

            LoginPassword = CreatePasswordBox();
            LoginPanel.Children.Add(LoginPassword);
            LoginPanel.Children.Add(new FrameworkElement { Height = 20 });

            var loginBtn = new Button
            {
                Content = "Bejelentkezés",
                Margin = new Thickness(0, 0, 0, 16),
                FontSize = 15,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(0x0A, 0x05, 0x00)),
                Padding = new Thickness(0, 14, 0, 14),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };
            loginBtn.Background = new LinearGradientBrush(
                Color.FromRgb(0xFF, 0xAB, 0x35),
                Color.FromRgb(0xB3, 0x55, 0x00), 90);
            loginBtn.Click += LoginButton_Click;
            LoginPanel.Children.Add(loginBtn);

            // Elválasztó
            var separatorGrid = new Grid { Margin = new Thickness(0, 6, 0, 10) };
            separatorGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            separatorGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            separatorGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var sepLeft = new Border
            {
                Height = 1,
                Background = new SolidColorBrush(Color.FromArgb(0x33, 0xFF, 0x8C, 0x00)),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(sepLeft, 0);

            var sepText = new TextBlock
            {
                Text = "VAGY",
                Foreground = new SolidColorBrush(Color.FromArgb(0x80, 0xFF, 0x8C, 0x00)),
                FontSize = 11,
                Margin = new Thickness(14, 0, 14, 0)
            };
            Grid.SetColumn(sepText, 1);

            var sepRight = new Border
            {
                Height = 1,
                Background = new SolidColorBrush(Color.FromArgb(0x33, 0xFF, 0x8C, 0x00)),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(sepRight, 2);

            separatorGrid.Children.Add(sepLeft);
            separatorGrid.Children.Add(sepText);
            separatorGrid.Children.Add(sepRight);
            LoginPanel.Children.Add(separatorGrid);

            var regBtn = new Button
            {
                Content = "Regisztráció",
                Background = Brushes.Transparent,
                Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x9A, 0x1F)),
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Padding = new Thickness(16, 10, 16, 10),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x4D, 0xFF, 0x8C, 0x00)),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand
            };
            regBtn.Click += SwitchToRegister_Click;
            LoginPanel.Children.Add(regBtn);

            mainStack.Children.Add(LoginPanel);

            // ---- Register Panel ----
            RegisterPanel = new StackPanel { Visibility = Visibility.Collapsed };

            RegUsername = CreateInput("Felhasználónév");
            RegisterPanel.Children.Add(RegUsername);
            RegisterPanel.Children.Add(new FrameworkElement { Height = 12 });

            RegEmail = CreateInput("Email");
            RegisterPanel.Children.Add(RegEmail);
            RegisterPanel.Children.Add(new FrameworkElement { Height = 12 });

            RegPassword = CreatePasswordBox();
            RegisterPanel.Children.Add(RegPassword);
            RegisterPanel.Children.Add(new FrameworkElement { Height = 12 });

            RegPassword2 = CreatePasswordBox();
            RegisterPanel.Children.Add(RegPassword2);
            RegisterPanel.Children.Add(new FrameworkElement { Height = 20 });

            var regSubmitBtn = new Button
            {
                Content = "Regisztráció",
                Margin = new Thickness(0, 0, 0, 16),
                FontSize = 15,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(0x0A, 0x05, 0x00)),
                Padding = new Thickness(0, 14, 0, 14),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };
            regSubmitBtn.Background = new LinearGradientBrush(
                Color.FromRgb(0xFF, 0xAB, 0x35),
                Color.FromRgb(0xB3, 0x55, 0x00), 90);
            regSubmitBtn.Click += RegisterButton_Click;
            RegisterPanel.Children.Add(regSubmitBtn);

            var backBtn = new Button
            {
                Content = "Vissza a bejelentkezéshez",
                Background = Brushes.Transparent,
                Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x9A, 0x1F)),
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Padding = new Thickness(16, 10, 16, 10),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x4D, 0xFF, 0x8C, 0x00)),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand
            };
            backBtn.Click += SwitchToLogin_Click;
            RegisterPanel.Children.Add(backBtn);

            mainStack.Children.Add(RegisterPanel);

            outerGrid.Children.Add(card);
            Content = outerGrid;
        }

        private TextBox CreateInput(string placeholder)
        {
            var tb = new TextBox
            {
                Background = new SolidColorBrush(Color.FromRgb(0x2D, 0x24, 0x18)),
                Foreground = new SolidColorBrush(Color.FromRgb(0x8A, 0x7A, 0x65)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x3D, 0x35, 0x28)),
                BorderThickness = new Thickness(1),
                FontSize = 14,
                Padding = new Thickness(14, 12, 14, 12),
                Text = placeholder,
                CaretBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0x9A, 0x1F))
            };

            tb.GotFocus += (s, e) =>
            {
                if (tb.Text == placeholder)
                {
                    tb.Text = "";
                    tb.Foreground = new SolidColorBrush(Color.FromRgb(0xF5, 0xF0, 0xE8));
                }
            };
            tb.LostFocus += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(tb.Text))
                {
                    tb.Text = placeholder;
                    tb.Foreground = new SolidColorBrush(Color.FromRgb(0x8A, 0x7A, 0x65));
                }
            };

            return tb;
        }

        private PasswordBox CreatePasswordBox()
        {
            return new PasswordBox
            {
                Background = new SolidColorBrush(Color.FromRgb(0x2D, 0x24, 0x18)),
                Foreground = new SolidColorBrush(Color.FromRgb(0xF5, 0xF0, 0xE8)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x3D, 0x35, 0x28)),
                BorderThickness = new Thickness(1),
                FontSize = 14,
                Padding = new Thickness(14, 12, 14, 12),
                CaretBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0x9A, 0x1F))
            };
        }

        private void ShowError(string message)
        {
            ErrorText.Text = message;
            ErrorBorder.Visibility = Visibility.Visible;
            var timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(5)
            };
            timer.Tick += (s, e) =>
            {
                ErrorBorder.Visibility = Visibility.Collapsed;
                ((System.Windows.Threading.DispatcherTimer)s).Stop();
            };
            timer.Start();
        }

        private void ShowSuccess(string message)
        {
            MessageBox.Show(message, "Siker", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void SwitchToRegister_Click(object sender, RoutedEventArgs e)
        {
            LoginPanel.Visibility = Visibility.Collapsed;
            RegisterPanel.Visibility = Visibility.Visible;
            SubtitleText.Text = "Regisztráció";
            ErrorBorder.Visibility = Visibility.Collapsed;
        }

        private void SwitchToLogin_Click(object sender, RoutedEventArgs e)
        {
            RegisterPanel.Visibility = Visibility.Collapsed;
            LoginPanel.Visibility = Visibility.Visible;
            SubtitleText.Text = "Bejelentkezés";
            ErrorBorder.Visibility = Visibility.Collapsed;
        }

        private void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            string username = LoginUsername.Text.Trim();
            string password = LoginPassword.Password;

            if (string.IsNullOrWhiteSpace(username) || username == "Felhasználónév vagy email")
            {
                ShowError("Add meg a felhasználónevet vagy email címet!");
                return;
            }
            if (string.IsNullOrWhiteSpace(password))
            {
                ShowError("Add meg a jelszót!");
                return;
            }

            try
            {
                using (var conn = _mainWindow.GetConnection())
                using (var cmd = new MySqlCommand(
                    @"SELECT users.id, users.username, passwords.password_hash 
                      FROM users 
                      JOIN passwords ON users.password_id = passwords.id 
                      WHERE users.email = @login OR users.username = @login", conn))
                {
                    cmd.Parameters.AddWithValue("@login", username);
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            string hash = reader.GetString("password_hash");
                            int uid = reader.GetInt32("id");
                            string uname = reader.GetString("username");
                            reader.Close();

                            if (_mainWindow.BCryptVerify(password, hash))
                            {
                                MainWindow.LoggedInUserId = uid;
                                MainWindow.LoggedInUsername = uname;

                                // Admin ellenőrzés
                                using (var adminCmd = new MySqlCommand(
                                    "SELECT COUNT(*) FROM admins WHERE user_id = @uid", conn))
                                {
                                    adminCmd.Parameters.AddWithValue("@uid", uid);
                                    MainWindow.IsAdmin = Convert.ToInt32(adminCmd.ExecuteScalar()) > 0;
                                }

                                _mainWindow.NavigateToMain();
                            }
                            else
                            {
                                ShowError("Hibás jelszó!");
                            }
                        }
                        else
                        {
                            ShowError("Nem létező felhasználó!");
                        }
                    }
                }
            }
            catch (MySqlException ex)
            {
                ShowError("Adatbázis hiba: " + ex.Message);
            }
        }

        private void RegisterButton_Click(object sender, RoutedEventArgs e)
        {
            string username = RegUsername.Text.Trim();
            string email = RegEmail.Text.Trim();
            string password = RegPassword.Password;
            string password2 = RegPassword2.Password;

            if (string.IsNullOrWhiteSpace(username) || username == "Felhasználónév")
            {
                ShowError("Add meg a felhasználónevet!");
                return;
            }
            if (string.IsNullOrWhiteSpace(email) || email == "Email" || !email.Contains("@"))
            {
                ShowError("Érvénytelen email cím!");
                return;
            }
            if (string.IsNullOrWhiteSpace(password))
            {
                ShowError("Add meg a jelszót!");
                return;
            }
            if (password != password2)
            {
                ShowError("A jelszavak nem egyeznek!");
                return;
            }
            if (password.Length < 6)
            {
                ShowError("A jelszónak legalább 6 karakter hosszúnak kell lennie!");
                return;
            }

            // VIZSGALOCK ellenőrzés
            if (_mainWindow.CheckVizsgalock())
            {
                ShowError("Regisztráció jelenleg nem lehetséges (VIZSGALOCK aktív).");
                return;
            }

            try
            {
                using (var conn = _mainWindow.GetConnection())
                {
                    // Foglaltság ellenőrzése
                    using (var checkCmd = new MySqlCommand(
                        "SELECT email, username FROM users WHERE email = @email OR username = @username LIMIT 1", conn))
                    {
                        checkCmd.Parameters.AddWithValue("@email", email);
                        checkCmd.Parameters.AddWithValue("@username", username);
                        using (var reader = checkCmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                string existingEmail = reader.GetString("email");
                                if (existingEmail == email)
                                    ShowError("Ez az email cím már foglalt!");
                                else
                                    ShowError("Ez a felhasználónév már foglalt!");
                                return;
                            }
                        }
                    }

                    string hash = _mainWindow.BCryptHash(password);
                    using (var transaction = conn.BeginTransaction())
                    {
                        try
                        {
                            long passwordId;
                            using (var pwdCmd = new MySqlCommand(
                                "INSERT INTO passwords (password_hash) VALUES (@hash); SELECT LAST_INSERT_ID();",
                                conn, transaction))
                            {
                                pwdCmd.Parameters.AddWithValue("@hash", hash);
                                passwordId = Convert.ToInt64(pwdCmd.ExecuteScalar());
                            }
                            using (var userCmd = new MySqlCommand(
                                "INSERT INTO users (email, username, password_id) VALUES (@email, @username, @pwdId)",
                                conn, transaction))
                            {
                                userCmd.Parameters.AddWithValue("@email", email);
                                userCmd.Parameters.AddWithValue("@username", username);
                                userCmd.Parameters.AddWithValue("@pwdId", passwordId);
                                userCmd.ExecuteNonQuery();
                            }
                            transaction.Commit();
                            ShowSuccess("Sikeres regisztráció! Most már bejelentkezhetsz.");
                            SwitchToLogin_Click(null, null);
                            LoginUsername.Text = username;
                            LoginUsername.Foreground = new SolidColorBrush(Color.FromRgb(0xF5, 0xF0, 0xE8));
                        }
                        catch (Exception)
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                }
            }
            catch (MySqlException ex)
            {
                ShowError("Adatbázis hiba: " + ex.Message);
            }
        }
    }
}

// ============================================
// FŐOLDAL (TERMÉKEK LISTÁJA)
// ============================================
namespace cucisstuff
{
    public class MainPage : Page
    {
        private readonly MainWindow _mainWindow;
        private WrapPanel ItemsWrapPanel;
        private TextBox SearchBox;
        private List<ItemViewModel> allItems = new List<ItemViewModel>();

        public MainPage(MainWindow mainWindow)
        {
            _mainWindow = mainWindow;
            InitializeUI();
            Loaded += (s, e) => LoadItems();
        }

        private void InitializeUI()
        {
            Background = new SolidColorBrush(Color.FromRgb(0x05, 0x05, 0x05));

            var dockPanel = new DockPanel();
            Content = dockPanel;

            // Top bar
            var topBar = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x0A, 0x0A, 0x0A)),
                Padding = new Thickness(10, 10, 10, 10)
            };
            DockPanel.SetDock(topBar, Dock.Top);

            var topGrid = new Grid();
            topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Feltöltés gomb
            var uploadBtn = new Button
            {
                Content = "＋ Hirdetés feladása",
                Background = Brushes.Transparent,
                Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x9A, 0x1F)),
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Padding = new Thickness(16, 10, 16, 10),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x4D, 0xFF, 0x8C, 0x00)),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand,
                Margin = new Thickness(0, 0, 10, 0)
            };
            uploadBtn.Click += (s, e) => _mainWindow.NavigateToUpload();
            Grid.SetColumn(uploadBtn, 0);

            // Keresőmező
            SearchBox = new TextBox
            {
                Background = new SolidColorBrush(Color.FromRgb(0x2D, 0x24, 0x18)),
                Foreground = new SolidColorBrush(Color.FromRgb(0x8A, 0x7A, 0x65)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x3D, 0x35, 0x28)),
                BorderThickness = new Thickness(1),
                FontSize = 14,
                Padding = new Thickness(14, 10, 14, 10),
                Margin = new Thickness(10, 0, 10, 0),
                Text = "Keresés..."
            };
            SearchBox.GotFocus += (s, e) =>
            {
                if (SearchBox.Text == "Keresés...")
                {
                    SearchBox.Text = "";
                    SearchBox.Foreground = new SolidColorBrush(Color.FromRgb(0xF5, 0xF0, 0xE8));
                }
            };
            SearchBox.LostFocus += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(SearchBox.Text))
                {
                    SearchBox.Text = "Keresés...";
                    SearchBox.Foreground = new SolidColorBrush(Color.FromRgb(0x8A, 0x7A, 0x65));
                }
            };
            SearchBox.TextChanged += (s, e) => FilterItems();
            Grid.SetColumn(SearchBox, 1);

            // Fiók gomb
            var accountBtn = new Button
            {
                Content = "⚙️ FIÓK",
                Background = Brushes.Transparent,
                Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x9A, 0x1F)),
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Padding = new Thickness(16, 10, 16, 10),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x4D, 0xFF, 0x8C, 0x00)),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand,
                Margin = new Thickness(0, 0, 10, 0)
            };
            accountBtn.Click += (s, e) => _mainWindow.NavigateToAccount();
            Grid.SetColumn(accountBtn, 2);

            // Üzenetek gomb
            var messagesBtn = new Button
            {
                Content = "💬 Üzenetek",
                Background = Brushes.Transparent,
                Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x9A, 0x1F)),
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Padding = new Thickness(16, 10, 16, 10),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x4D, 0xFF, 0x8C, 0x00)),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand,
                Margin = new Thickness(0, 0, 10, 0)
            };
            messagesBtn.Click += (s, e) => _mainWindow.NavigateToMessages();
            Grid.SetColumn(messagesBtn, 3);

            // Admin gomb (csak ha admin) vagy kijelentkezés gomb
            if (MainWindow.IsAdmin)
            {
                var adminBtn = new Button
                {
                    Content = "🛡️ Admin",
                    Background = new SolidColorBrush(Color.FromArgb(0x1F, 0xFF, 0xD7, 0x00)),
                    Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0xD7, 0x00)),
                    FontSize = 14,
                    FontWeight = FontWeights.Bold,
                    Padding = new Thickness(16, 10, 16, 10),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(0x4D, 0xFF, 0xD7, 0x00)),
                    BorderThickness = new Thickness(1),
                    Cursor = Cursors.Hand
                };
                adminBtn.Click += (s, e) => _mainWindow.NavigateToAdmin();
                Grid.SetColumn(adminBtn, 4);
            }

            // Kijelentkezés gomb
            var logoutBtn = new Button
            {
                Content = "🚪 Kilépés",
                Background = Brushes.Transparent,
                Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x44, 0x44)),
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Padding = new Thickness(16, 10, 16, 10),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x4D, 0xFF, 0x44, 0x44)),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand,
                Margin = new Thickness(10, 0, 0, 0)
            };
            logoutBtn.Click += (s, e) => _mainWindow.NavigateToLogin();
            Grid.SetColumn(logoutBtn, 4);

            topGrid.Children.Add(uploadBtn);
            topGrid.Children.Add(SearchBox);
            topGrid.Children.Add(accountBtn);
            topGrid.Children.Add(messagesBtn);
            topGrid.Children.Add(logoutBtn);
            topBar.Child = topGrid;
            dockPanel.Children.Add(topBar);

            // Termék lista
            var scrollViewer = new ScrollViewer();
            ItemsWrapPanel = new WrapPanel { Margin = new Thickness(20, 20, 20, 20) };
            scrollViewer.Content = ItemsWrapPanel;
            dockPanel.Children.Add(scrollViewer);
        }

        private void LoadItems()
        {
            allItems.Clear();
            try
            {
                using (var conn = _mainWindow.GetConnection())
                using (var cmd = new MySqlCommand(
                    @"SELECT i.id, i.title, i.price, i.description, i.created_at, i.sold,
                             i.user_id, u.username AS seller_name,
                             (SELECT image_path FROM item_images WHERE item_id = i.id AND is_primary = 1 LIMIT 1) AS first_image
                      FROM items i
                      JOIN users u ON i.user_id = u.id
                      WHERE i.sold = FALSE
                      ORDER BY i.created_at DESC", conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var item = new ItemViewModel
                        {
                            Id = reader.GetString("id"),
                            Title = reader.GetString("title"),
                            Price = reader.GetDecimal("price"),
                            Description = reader.IsDBNull(reader.GetOrdinal("description")) ? "" : reader.GetString("description"),
                            CreatedAt = reader.GetDateTime("created_at"),
                            IsSold = reader.GetBoolean("sold"),
                            SellerId = reader.GetInt32("user_id"),
                            SellerName = reader.GetString("seller_name"),
                            FirstImagePath = reader.IsDBNull(reader.GetOrdinal("first_image")) ? null : reader.GetString("first_image")
                        };
                        allItems.Add(item);
                    }
                }

                // Képek betöltése
                foreach (var item in allItems)
                {
                    try
                    {
                        using (var conn = _mainWindow.GetConnection())
                        using (var imgCmd = new MySqlCommand(
                            "SELECT image_path FROM item_images WHERE item_id = @id ORDER BY sort_order", conn))
                        {
                            imgCmd.Parameters.AddWithValue("@id", item.Id);
                            using (var imgReader = imgCmd.ExecuteReader())
                            {
                                while (imgReader.Read())
                                {
                                    item.AllImagePaths.Add(imgReader.GetString("image_path"));
                                }
                            }
                        }
                    }
                    catch { }
                }
            }
            catch (MySqlException ex)
            {
                MessageBox.Show("Adatbázis hiba: " + ex.Message);
            }
            DisplayItems(allItems);
        }

        private void DisplayItems(IEnumerable<ItemViewModel> items)
        {
            ItemsWrapPanel.Children.Clear();
            foreach (var item in items)
            {
                var card = CreateItemCard(item);
                ItemsWrapPanel.Children.Add(card);
            }
        }

        private Border CreateItemCard(ItemViewModel item)
        {
            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x0F, 0x0F, 0x0F)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x29, 0xFF, 0x8C, 0x00)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Width = 180,
                Margin = new Thickness(6, 6, 6, 6),
                Cursor = Cursors.Hand,
                Tag = item
            };
            border.MouseLeftButtonDown += ItemCard_Click;
            border.MouseEnter += (s, e) => border.BorderBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0x8C, 0x00));
            border.MouseLeave += (s, e) => border.BorderBrush = new SolidColorBrush(Color.FromArgb(0x29, 0xFF, 0x8C, 0x00));

            var stack = new StackPanel();

            // Kép
            if (!string.IsNullOrEmpty(item.FirstImagePath) && File.Exists(item.FirstImagePath))
            {
                var img = new Image
                {
                    Width = 160,
                    Height = 160,
                    Stretch = Stretch.UniformToFill
                };
                try
                {
                    img.Source = new BitmapImage(new Uri(item.FirstImagePath, UriKind.Absolute));
                }
                catch { }
                stack.Children.Add(img);
            }
            else
            {
                var placeholder = new Border
                {
                    Width = 160,
                    Height = 160,
                    Background = new SolidColorBrush(Color.FromArgb(0x1A, 0xFF, 0x8C, 0x00)),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(0x29, 0xFF, 0x8C, 0x00)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(8)
                };
                placeholder.Child = new TextBlock
                {
                    Text = "📷",
                    FontSize = 32,
                    Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x9A, 0x1F)),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                stack.Children.Add(placeholder);
            }

            // Képszám badge
            if (item.ImageCount > 1)
            {
                var badge = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(0xBF, 0x00, 0x00, 0x00)),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(0x4D, 0xFF, 0x8C, 0x00)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(10),
                    Padding = new Thickness(8, 3, 8, 3),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Margin = new Thickness(4, -24, 0, 0)
                };
                badge.Child = new TextBlock
                {
                    Text = $"+{item.ImageCount - 1} kép",
                    FontSize = 11,
                    Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x9A, 0x1F)),
                    FontWeight = FontWeights.Bold
                };
                stack.Children.Add(badge);
            }

            stack.Children.Add(new TextBlock
            {
                Text = item.Title,
                Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x9A, 0x1F)),
                FontWeight = FontWeights.Bold,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 6, 0, 2)
            });
            stack.Children.Add(new TextBlock
            {
                Text = item.PriceFormatted,
                Foreground = new SolidColorBrush(Color.FromRgb(0xF5, 0xF0, 0xE8)),
                FontSize = 16,
                Margin = new Thickness(0, 0, 0, 4)
            });
            stack.Children.Add(new TextBlock
            {
                Text = item.SellerName,
                Foreground = new SolidColorBrush(Color.FromRgb(0x8A, 0x7A, 0x65)),
                FontSize = 12,
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            stack.Children.Add(new TextBlock
            {
                Text = item.CreatedAtFormatted,
                Foreground = new SolidColorBrush(Color.FromArgb(0x80, 0xFF, 0xFF, 0xFF)),
                FontSize = 11,
                Margin = new Thickness(0, 2, 0, 0)
            });

            border.Child = stack;
            return border;
        }

        private void ItemCard_Click(object sender, MouseButtonEventArgs e)
        {
            var border = sender as Border;
            if (border?.Tag is ItemViewModel item)
            {
                var detailWindow = new ProductDetailWindow(_mainWindow, item);
                detailWindow.Owner = Window.GetWindow(this);
                detailWindow.ShowDialog();
            }
        }

        private void FilterItems()
        {
            string query = SearchBox.Text.Trim().ToLower();
            if (string.IsNullOrEmpty(query) || query == "keresés...")
                DisplayItems(allItems);
            else
                DisplayItems(allItems.Where(i =>
                    i.Title.ToLower().Contains(query) ||
                    i.SellerName.ToLower().Contains(query) ||
                    i.Description.ToLower().Contains(query)));
        }
    }
}

// ============================================
// TERMÉK RÉSZLETEK MODÁL ABLAK
// ============================================
namespace cucisstuff
{
    public class ProductDetailWindow : Window
    {
        private readonly MainWindow _mainWindow;
        private readonly ItemViewModel _item;
        private Image MainImage;
        private TextBlock NoImagePlaceholder;
        private int currentImageIndex = 0;
        private WrapPanel ThumbnailsPanel;
        private Button PrevBtn, NextBtn;

        public ProductDetailWindow(MainWindow mainWindow, ItemViewModel item)
        {
            _mainWindow = mainWindow;
            _item = item;

            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            Width = 900;
            Height = 650;
            ResizeMode = ResizeMode.CanResize;

            InitializeUI();
            LoadAllImages();
        }

        private void InitializeUI()
        {
            var mainBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0xF5, 0x05, 0x05, 0x05)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x29, 0xFF, 0x8C, 0x00)),
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(16)
            };

            var outerGrid = new Grid();
            mainBorder.Child = outerGrid;

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.5, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            outerGrid.Children.Add(grid);

            // Bal oldal: galéria
            var galleryPanel = new Grid { Margin = new Thickness(10, 10, 10, 10) };
            galleryPanel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            galleryPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Kép konténer
            var imageContainer = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0x80, 0x00, 0x00, 0x00)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x29, 0xFF, 0x8C, 0x00)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12)
            };
            Grid.SetRow(imageContainer, 0);

            var imageGrid = new Grid();
            MainImage = new Image
            {
                Stretch = Stretch.Uniform,
                Visibility = Visibility.Collapsed,
                Cursor = Cursors.Hand
            };
            NoImagePlaceholder = new TextBlock
            {
                Text = "📷 Nincs kép",
                FontSize = 18,
                Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x9A, 0x1F)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Visibility = Visibility.Visible
            };
            imageGrid.Children.Add(MainImage);
            imageGrid.Children.Add(NoImagePlaceholder);

            // Navigációs gombok
            PrevBtn = new Button
            {
                Content = "❮",
                Background = Brushes.Transparent,
                Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x9A, 0x1F)),
                FontSize = 24,
                Width = 40,
                Height = 40,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(10, 0, 0, 0),
                Cursor = Cursors.Hand,
                BorderThickness = new Thickness(0)
            };
            PrevBtn.Click += (s, e) => NavigateImage(-1);
            NextBtn = new Button
            {
                Content = "❯",
                Background = Brushes.Transparent,
                Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x9A, 0x1F)),
                FontSize = 24,
                Width = 40,
                Height = 40,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0),
                Cursor = Cursors.Hand,
                BorderThickness = new Thickness(0)
            };
            NextBtn.Click += (s, e) => NavigateImage(1);

            imageGrid.Children.Add(PrevBtn);
            imageGrid.Children.Add(NextBtn);
            imageContainer.Child = imageGrid;
            galleryPanel.Children.Add(imageContainer);

            // Bélyegképek
            var thumbScroll = new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Height = 80,
                Margin = new Thickness(0, 8, 0, 0)
            };
            ThumbnailsPanel = new WrapPanel();
            thumbScroll.Content = ThumbnailsPanel;
            Grid.SetRow(thumbScroll, 1);
            galleryPanel.Children.Add(thumbScroll);

            Grid.SetColumn(galleryPanel, 0);

            // Jobb oldal: termék adatok
            var detailsPanel = new StackPanel { Margin = new Thickness(20, 20, 20, 20) };

            detailsPanel.Children.Add(new TextBlock
            {
                Text = _item.Title,
                FontSize = 28,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x9A, 0x1F)),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 12)
            });

            detailsPanel.Children.Add(new TextBlock
            {
                Text = _item.PriceFormatted,
                FontSize = 32,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x9A, 0x1F)),
                Margin = new Thickness(0, 0, 0, 8)
            });

            var sellerText = new TextBlock
            {
                FontSize = 14,
                Foreground = new SolidColorBrush(Color.FromRgb(0xF5, 0xF0, 0xE8)),
                Margin = new Thickness(0, 0, 0, 4)
            };
            sellerText.Inlines.Add(new System.Windows.Documents.Run("Eladó: "));
            sellerText.Inlines.Add(new System.Windows.Documents.Run(_item.SellerName)
            {
                Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x9A, 0x1F)),
                FontWeight = FontWeights.Bold,
                FontSize = 16
            });
            sellerText.Cursor = Cursors.Hand;
            sellerText.MouseLeftButtonDown += (s, e) =>
            {
                MessageBox.Show($"Eladó: {_item.SellerName}\nTag azóta: {_item.CreatedAtFormatted}",
                    "Eladó profilja", MessageBoxButton.OK, MessageBoxImage.Information);
            };
            detailsPanel.Children.Add(sellerText);

            detailsPanel.Children.Add(new TextBlock
            {
                Text = _item.CreatedAtFormatted,
                FontSize = 13,
                Foreground = new SolidColorBrush(Color.FromArgb(0x66, 0xFF, 0xFF, 0xFF)),
                Margin = new Thickness(0, 0, 0, 16)
            });

            var descBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0x80, 0x00, 0x00, 0x00)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x29, 0xFF, 0x8C, 0x00)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(16, 16, 16, 16),
                MaxHeight = 200,
                Margin = new Thickness(0, 0, 0, 16)
            };
            var scrollDesc = new ScrollViewer();
            scrollDesc.Content = new TextBlock
            {
                Text = string.IsNullOrEmpty(_item.Description) ? "Nincs leírás." : _item.Description,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 14,
                Foreground = new SolidColorBrush(Color.FromRgb(0xF5, 0xF0, 0xE8)),
                LineHeight = 22
            };
            descBorder.Child = scrollDesc;
            detailsPanel.Children.Add(descBorder);

            // Vásárlás gomb
            var buyBtn = new Button
            {
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Padding = new Thickness(0, 16, 0, 16),
                Margin = new Thickness(0, 16, 0, 0),
                Cursor = Cursors.Hand,
                BorderThickness = new Thickness(0)
            };

            if (_item.IsSold)
            {
                buyBtn.Content = "Elkelt";
                buyBtn.Background = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55));
                buyBtn.Foreground = new SolidColorBrush(Color.FromRgb(0xAA, 0xAA, 0xAA));
                buyBtn.IsEnabled = false;
            }
            else
            {
                buyBtn.Content = "🛒 Vásárlás";
                buyBtn.Background = new LinearGradientBrush(
                    Color.FromRgb(0x00, 0xC8, 0x51),
                    Color.FromRgb(0x00, 0x7E, 0x33), 45);
                buyBtn.Foreground = new SolidColorBrush(Colors.White);
                buyBtn.Click += (s, ev) =>
                {
                    Close();
                    _mainWindow.NavigateToPurchase(_item.Id);
                };
            }

            // Stílus hozzárendelése a gombhoz - Template beállítása
            var templateBtn = new Button();
            var style = new Style(typeof(Button));
            style.Setters.Add(new Setter(Button.BackgroundProperty, buyBtn.Background));
            style.Setters.Add(new Setter(Button.ForegroundProperty, buyBtn.Foreground));
            style.Setters.Add(new Setter(Button.FontSizeProperty, buyBtn.FontSize));
            style.Setters.Add(new Setter(Button.FontWeightProperty, buyBtn.FontWeight));
            style.Setters.Add(new Setter(Button.PaddingProperty, buyBtn.Padding));
            style.Setters.Add(new Setter(Button.MarginProperty, buyBtn.Margin));
            style.Setters.Add(new Setter(Button.CursorProperty, buyBtn.Cursor));
            style.Setters.Add(new Setter(Button.BorderThicknessProperty, buyBtn.BorderThickness));
            style.Setters.Add(new Setter(Button.ContentProperty, buyBtn.Content));
            style.Setters.Add(new Setter(Button.IsEnabledProperty, buyBtn.IsEnabled));

            var controlTemplate = new ControlTemplate(typeof(Button));
            var frameworkElementFactory = new FrameworkElementFactory(typeof(Border));
            frameworkElementFactory.Name = "border";
            frameworkElementFactory.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
            frameworkElementFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(14));
            frameworkElementFactory.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Button.PaddingProperty));
            frameworkElementFactory.AppendChild(new FrameworkElementFactory(typeof(ContentPresenter)));
            controlTemplate.VisualTree = frameworkElementFactory;

            var trigger = new Trigger { Property = Button.IsMouseOverProperty, Value = true };
            trigger.Setters.Add(new Setter(Border.BackgroundProperty, buyBtn.Background, "border"));
            controlTemplate.Triggers.Add(trigger);

            style.Setters.Add(new Setter(Button.TemplateProperty, controlTemplate));
            buyBtn.Style = style;

            // Bezáró gomb az overlay-re
            var closeBtn = new Button
            {
                Content = "✕",
                Background = Brushes.Transparent,
                Foreground = new SolidColorBrush(Color.FromArgb(0x99, 0xFF, 0xFF, 0xFF)),
                FontSize = 18,
                Width = 40,
                Height = 40,
                Cursor = Cursors.Hand,
                BorderThickness = new Thickness(0),
                Margin = new Thickness(0, 8, 8, 0)
            };
            closeBtn.Click += (s, e) => Close();

            outerGrid.Children.Add(closeBtn);
            Panel.SetZIndex(closeBtn, 10);

            detailsPanel.Children.Add(buyBtn);
            Grid.SetColumn(detailsPanel, 1);
            grid.Children.Add(detailsPanel);

            Content = mainBorder;
        }

        private void LoadAllImages()
        {
            try
            {
                using (var conn = _mainWindow.GetConnection())
                using (var cmd = new MySqlCommand(
                    "SELECT image_path FROM item_images WHERE item_id = @id ORDER BY sort_order", conn))
                {
                    cmd.Parameters.AddWithValue("@id", _item.Id);
                    using (var reader = cmd.ExecuteReader())
                    {
                        _item.AllImagePaths.Clear();
                        while (reader.Read())
                            _item.AllImagePaths.Add(reader.GetString("image_path"));
                    }
                }
            }
            catch { }

            UpdateImageDisplay();
            UpdateThumbnails();
        }

        private void UpdateImageDisplay()
        {
            if (_item.AllImagePaths.Count > 0 && currentImageIndex < _item.AllImagePaths.Count)
            {
                string path = _item.AllImagePaths[currentImageIndex];
                if (File.Exists(path))
                {
                    try
                    {
                        MainImage.Source = new BitmapImage(new Uri(path, UriKind.Absolute));
                        MainImage.Visibility = Visibility.Visible;
                        NoImagePlaceholder.Visibility = Visibility.Collapsed;
                    }
                    catch
                    {
                        MainImage.Visibility = Visibility.Collapsed;
                        NoImagePlaceholder.Visibility = Visibility.Visible;
                    }
                }
                else
                {
                    MainImage.Visibility = Visibility.Collapsed;
                    NoImagePlaceholder.Visibility = Visibility.Visible;
                }
            }
            else
            {
                MainImage.Visibility = Visibility.Collapsed;
                NoImagePlaceholder.Visibility = Visibility.Visible;
            }
            PrevBtn.Visibility = _item.AllImagePaths.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
            NextBtn.Visibility = _item.AllImagePaths.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void UpdateThumbnails()
        {
            ThumbnailsPanel.Children.Clear();
            for (int i = 0; i < _item.AllImagePaths.Count; i++)
            {
                int index = i;
                var thumbBorder = new Border
                {
                    Width = 64,
                    Height = 64,
                    Margin = new Thickness(3, 3, 3, 3),
                    BorderBrush = i == currentImageIndex
                        ? new SolidColorBrush(Color.FromRgb(0xFF, 0x8C, 0x00))
                        : new SolidColorBrush(Color.FromArgb(0x29, 0xFF, 0x8C, 0x00)),
                    BorderThickness = new Thickness(2),
                    CornerRadius = new CornerRadius(6),
                    Cursor = Cursors.Hand,
                    Background = new SolidColorBrush(Color.FromArgb(0x80, 0x00, 0x00, 0x00))
                };
                if (File.Exists(_item.AllImagePaths[i]))
                {
                    try
                    {
                        thumbBorder.Child = new Image
                        {
                            Source = new BitmapImage(new Uri(_item.AllImagePaths[i], UriKind.Absolute)),
                            Stretch = Stretch.UniformToFill
                        };
                    }
                    catch { }
                }
                thumbBorder.MouseLeftButtonDown += (s, e) =>
                {
                    currentImageIndex = index;
                    UpdateImageDisplay();
                    UpdateThumbnails();
                };
                ThumbnailsPanel.Children.Add(thumbBorder);
            }
        }

        private void NavigateImage(int direction)
        {
            if (_item.AllImagePaths.Count == 0) return;
            currentImageIndex = (currentImageIndex + direction + _item.AllImagePaths.Count) % _item.AllImagePaths.Count;
            UpdateImageDisplay();
            UpdateThumbnails();
        }
    }
}

// ============================================
// TOVÁBBI OLDALAK VÁZAI
// ============================================
namespace cucisstuff
{
    // Fiók oldal
    public class AccountPage : Page
    {
        private readonly MainWindow _mainWindow;
        public AccountPage(MainWindow mainWindow)
        {
            _mainWindow = mainWindow;
            InitializeUI();
            LoadUserData();
        }

        private void InitializeUI()
        {
            Background = new SolidColorBrush(Color.FromRgb(0x05, 0x05, 0x05));
            var scroll = new ScrollViewer();
            var stack = new StackPanel { Margin = new Thickness(40, 40, 40, 40) };

            var backBtn = new Button
            {
                Content = "← Vissza a főoldalra",
                Background = Brushes.Transparent,
                Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x9A, 0x1F)),
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Padding = new Thickness(16, 10, 16, 10),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x4D, 0xFF, 0x8C, 0x00)),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            backBtn.Click += (s, e) => _mainWindow.NavigateToMain();
            stack.Children.Add(backBtn);

            stack.Children.Add(new TextBlock
            {
                Text = "Fiók beállítások",
                FontSize = 22,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x9A, 0x1F)),
                Margin = new Thickness(0, 16, 0, 16)
            });

            // Felhasználónév
            stack.Children.Add(new TextBlock
            {
                Text = "Felhasználónév",
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x9A, 0x1F)),
                Margin = new Thickness(0, 0, 0, 6)
            });
            var usernameBox = new TextBox
            {
                Background = new SolidColorBrush(Color.FromRgb(0x2D, 0x24, 0x18)),
                Foreground = new SolidColorBrush(Color.FromRgb(0xF5, 0xF0, 0xE8)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x3D, 0x35, 0x28)),
                BorderThickness = new Thickness(1),
                FontSize = 14,
                Padding = new Thickness(14, 12, 14, 12),
                Margin = new Thickness(0, 0, 0, 12)
            };
            stack.Children.Add(usernameBox);

            // Email
            stack.Children.Add(new TextBlock
            {
                Text = "Email",
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x9A, 0x1F)),
                Margin = new Thickness(0, 0, 0, 6)
            });
            var emailBox = new TextBox
            {
                Background = new SolidColorBrush(Color.FromRgb(0x2D, 0x24, 0x18)),
                Foreground = new SolidColorBrush(Color.FromRgb(0xF5, 0xF0, 0xE8)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x3D, 0x35, 0x28)),
                BorderThickness = new Thickness(1),
                FontSize = 14,
                Padding = new Thickness(14, 12, 14, 12),
                Margin = new Thickness(0, 0, 0, 12)
            };
            stack.Children.Add(emailBox);

            // Jelszó
            stack.Children.Add(new TextBlock
            {
                Text = "Új jelszó (ha módosítani szeretnéd)",
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x9A, 0x1F)),
                Margin = new Thickness(0, 0, 0, 6)
            });
            var pwdBox = new PasswordBox
            {
                Background = new SolidColorBrush(Color.FromRgb(0x2D, 0x24, 0x18)),
                Foreground = new SolidColorBrush(Color.FromRgb(0xF5, 0xF0, 0xE8)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x3D, 0x35, 0x28)),
                BorderThickness = new Thickness(1),
                FontSize = 14,
                Padding = new Thickness(14, 12, 14, 12),
                Margin = new Thickness(0, 0, 0, 12)
            };
            stack.Children.Add(pwdBox);

            var saveBtn = new Button
            {
                Content = "Mentés",
                FontSize = 15,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(0x0A, 0x05, 0x00)),
                Padding = new Thickness(0, 14, 0, 14),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                Width = 200,
                Margin = new Thickness(0, 12, 0, 0)
            };
            saveBtn.Background = new LinearGradientBrush(
                Color.FromRgb(0xFF, 0xAB, 0x35),
                Color.FromRgb(0xB3, 0x55, 0x00), 90);
            saveBtn.Click += (s, e) => SaveUserData(usernameBox.Text, emailBox.Text, pwdBox.Password);
            stack.Children.Add(saveBtn);

            scroll.Content = stack;
            Content = scroll;
        }

        private void LoadUserData()
        {
            // Betöltési logika...
        }

        private void SaveUserData(string username, string email, string password)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                MessageBox.Show("A felhasználónév nem lehet üres!");
                return;
            }
            if (string.IsNullOrWhiteSpace(email) || !email.Contains("@"))
            {
                MessageBox.Show("Érvénytelen email cím!");
                return;
            }

            try
            {
                using (var conn = _mainWindow.GetConnection())
                {
                    // Foglaltság ellenőrzése
                    using (var checkCmd = new MySqlCommand(
                        "SELECT id FROM users WHERE (username = @uname OR email = @email) AND id != @uid",
                        conn))
                    {
                        checkCmd.Parameters.AddWithValue("@uname", username);
                        checkCmd.Parameters.AddWithValue("@email", email);
                        checkCmd.Parameters.AddWithValue("@uid", MainWindow.LoggedInUserId);
                        if (checkCmd.ExecuteScalar() != null)
                        {
                            MessageBox.Show("A felhasználónév vagy email már foglalt!");
                            return;
                        }
                    }

                    using (var cmd = new MySqlCommand(
                        "UPDATE users SET username = @uname, email = @email WHERE id = @uid", conn))
                    {
                        cmd.Parameters.AddWithValue("@uname", username);
                        cmd.Parameters.AddWithValue("@email", email);
                        cmd.Parameters.AddWithValue("@uid", MainWindow.LoggedInUserId);
                        cmd.ExecuteNonQuery();
                    }

                    // Jelszó módosítása ha meg van adva
                    if (!string.IsNullOrWhiteSpace(password) && password.Length >= 6)
                    {
                        string hash = _mainWindow.BCryptHash(password);
                        long pwdId;
                        using (var pwdCmd = new MySqlCommand(
                            "INSERT INTO passwords (password_hash) VALUES (@hash); SELECT LAST_INSERT_ID();", conn))
                        {
                            pwdCmd.Parameters.AddWithValue("@hash", hash);
                            pwdId = Convert.ToInt64(pwdCmd.ExecuteScalar());
                        }
                        using (var updCmd = new MySqlCommand(
                            "UPDATE users SET password_id = @pwdId WHERE id = @uid", conn))
                        {
                            updCmd.Parameters.AddWithValue("@pwdId", pwdId);
                            updCmd.Parameters.AddWithValue("@uid", MainWindow.LoggedInUserId);
                            updCmd.ExecuteNonQuery();
                        }
                    }

                    MainWindow.LoggedInUsername = username;
                    MessageBox.Show("Adatok mentve!", "Siker", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (MySqlException ex)
            {
                MessageBox.Show("Adatbázis hiba: " + ex.Message);
            }
        }
    }

    // Termék feltöltés oldal
    public class UploadPage : Page
    {
        private readonly MainWindow _mainWindow;
        private List<byte[]> selectedImages = new List<byte[]>();

        public UploadPage(MainWindow mainWindow)
        {
            _mainWindow = mainWindow;
            InitializeUI();
        }

        private void InitializeUI()
        {
            Background = new SolidColorBrush(Color.FromRgb(0x05, 0x05, 0x05));
            var scroll = new ScrollViewer();
            var stack = new StackPanel { Margin = new Thickness(40, 40, 40, 40) };

            var backBtn = new Button
            {
                Content = "← Vissza",
                Background = Brushes.Transparent,
                Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x9A, 0x1F)),
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Padding = new Thickness(16, 10, 16, 10),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x4D, 0xFF, 0x8C, 0x00)),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            backBtn.Click += (s, e) => _mainWindow.NavigateToMain();
            stack.Children.Add(backBtn);

            stack.Children.Add(new TextBlock
            {
                Text = "Új hirdetés",
                FontSize = 22,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x9A, 0x1F)),
                Margin = new Thickness(0, 16, 0, 8)
            });
            stack.Children.Add(new TextBlock
            {
                Text = "Tölts fel legalább 1 képet a termékről",
                Foreground = new SolidColorBrush(Color.FromRgb(0x8A, 0x7A, 0x65)),
                Margin = new Thickness(0, 0, 0, 16)
            });

            // Képfeltöltés gomb
            var imgBtn = new Button
            {
                Content = "📸 Képek kiválasztása",
                Background = Brushes.Transparent,
                Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x9A, 0x1F)),
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Padding = new Thickness(16, 10, 16, 10),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x4D, 0xFF, 0x8C, 0x00)),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand,
                Margin = new Thickness(0, 0, 0, 12)
            };
            imgBtn.Click += (s, e) =>
            {
                var openFileDialog = new OpenFileDialog
                {
                    Filter = "Képfájlok|*.jpg;*.jpeg;*.png;*.gif;*.webp",
                    Multiselect = true
                };
                if (openFileDialog.ShowDialog() == true)
                {
                    foreach (var file in openFileDialog.FileNames)
                    {
                        selectedImages.Add(File.ReadAllBytes(file));
                    }
                    MessageBox.Show($"{selectedImages.Count} kép kiválasztva.");
                }
            };
            stack.Children.Add(imgBtn);

            // Cím
            stack.Children.Add(new TextBlock
            {
                Text = "Cím *",
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x9A, 0x1F)),
                Margin = new Thickness(0, 0, 0, 6)
            });
            var titleBox = new TextBox
            {
                Background = new SolidColorBrush(Color.FromRgb(0x2D, 0x24, 0x18)),
                Foreground = new SolidColorBrush(Color.FromRgb(0xF5, 0xF0, 0xE8)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x3D, 0x35, 0x28)),
                BorderThickness = new Thickness(1),
                FontSize = 14,
                Padding = new Thickness(14, 12, 14, 12),
                Margin = new Thickness(0, 0, 0, 12)
            };
            stack.Children.Add(titleBox);

            // Leírás
            stack.Children.Add(new TextBlock
            {
                Text = "Leírás *",
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x9A, 0x1F)),
                Margin = new Thickness(0, 0, 0, 6)
            });
            var descBox = new TextBox
            {
                Background = new SolidColorBrush(Color.FromRgb(0x2D, 0x24, 0x18)),
                Foreground = new SolidColorBrush(Color.FromRgb(0xF5, 0xF0, 0xE8)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x3D, 0x35, 0x28)),
                BorderThickness = new Thickness(1),
                FontSize = 14,
                Padding = new Thickness(14, 12, 14, 12),
                TextWrapping = TextWrapping.Wrap,
                AcceptsReturn = true,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Height = 120,
                Margin = new Thickness(0, 0, 0, 12)
            };
            stack.Children.Add(descBox);

            // Ár
            stack.Children.Add(new TextBlock
            {
                Text = "Ár (Ft) *",
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x9A, 0x1F)),
                Margin = new Thickness(0, 0, 0, 6)
            });
            var priceBox = new TextBox
            {
                Background = new SolidColorBrush(Color.FromRgb(0x2D, 0x24, 0x18)),
                Foreground = new SolidColorBrush(Color.FromRgb(0xF5, 0xF0, 0xE8)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x3D, 0x35, 0x28)),
                BorderThickness = new Thickness(1),
                FontSize = 14,
                Padding = new Thickness(14, 12, 14, 12),
                Margin = new Thickness(0, 0, 0, 12)
            };
            stack.Children.Add(priceBox);

            var submitBtn = new Button
            {
                Content = "Hirdetés feladása",
                FontSize = 15,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(0x0A, 0x05, 0x00)),
                Padding = new Thickness(0, 14, 0, 14),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };
            submitBtn.Background = new LinearGradientBrush(
                Color.FromRgb(0xFF, 0xAB, 0x35),
                Color.FromRgb(0xB3, 0x55, 0x00), 90);
            submitBtn.Click += (s, e) => SubmitItem(titleBox.Text, descBox.Text, priceBox.Text);
            stack.Children.Add(submitBtn);

            scroll.Content = stack;
            Content = scroll;
        }

        private void SubmitItem(string title, string desc, string priceText)
        {
            if (!_mainWindow.CanPerformWriteOperation())
            {
                MessageBox.Show("VIZSGALOCK aktív!", "Hiba", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(desc) ||
                !decimal.TryParse(priceText, out decimal price) || price < 0)
            {
                MessageBox.Show("Minden mezőt ki kell tölteni!", "Hiba", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            if (selectedImages.Count == 0)
            {
                MessageBox.Show("Legalább egy kép szükséges!", "Hiba", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                string itemId = _mainWindow.GenerateId();
                using (var conn = _mainWindow.GetConnection())
                using (var transaction = conn.BeginTransaction())
                {
                    try
                    {
                        using (var cmd = new MySqlCommand(
                            "INSERT INTO items (id, user_id, title, description, price) VALUES (@id, @uid, @title, @desc, @price)",
                            conn, transaction))
                        {
                            cmd.Parameters.AddWithValue("@id", itemId);
                            cmd.Parameters.AddWithValue("@uid", MainWindow.LoggedInUserId);
                            cmd.Parameters.AddWithValue("@title", title);
                            cmd.Parameters.AddWithValue("@desc", desc);
                            cmd.Parameters.AddWithValue("@price", price);
                            cmd.ExecuteNonQuery();
                        }

                        string uploadDir = Path.Combine("uploads", itemId);
                        Directory.CreateDirectory(uploadDir);
                        for (int i = 0; i < selectedImages.Count; i++)
                        {
                            string ext = ".jpg";
                            byte[] imgData = _mainWindow.ResizeImage(selectedImages[i]);
                            string filename = $"{Guid.NewGuid()}_{i}{ext}";
                            string filepath = Path.Combine(uploadDir, filename);
                            File.WriteAllBytes(filepath, imgData);

                            using (var imgCmd = new MySqlCommand(
                                "INSERT INTO item_images (item_id, image_path, image_filename, is_primary, sort_order) VALUES (@iid, @path, @fname, @primary, @sort)",
                                conn, transaction))
                            {
                                imgCmd.Parameters.AddWithValue("@iid", itemId);
                                imgCmd.Parameters.AddWithValue("@path", filepath);
                                imgCmd.Parameters.AddWithValue("@fname", filename);
                                imgCmd.Parameters.AddWithValue("@primary", i == 0);
                                imgCmd.Parameters.AddWithValue("@sort", i);
                                imgCmd.ExecuteNonQuery();
                            }
                        }
                        transaction.Commit();
                        MessageBox.Show("Hirdetés sikeresen feladva!", "Siker", MessageBoxButton.OK, MessageBoxImage.Information);
                        _mainWindow.NavigateToMain();
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Hiba: " + ex.Message, "Hiba", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    // Admin oldal
    public class AdminPage : Page
    {
        private readonly MainWindow _mainWindow;
        private TabControl TabControl;

        public AdminPage(MainWindow mainWindow)
        {
            _mainWindow = mainWindow;
            InitializeUI();
            LoadAdminData();
        }

        private void InitializeUI()
        {
            Background = new SolidColorBrush(Color.FromRgb(0x05, 0x05, 0x05));
            var dockPanel = new DockPanel();

            var topBar = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x0A, 0x0A, 0x0A)),
                Padding = new Thickness(10, 10, 10, 10)
            };
            DockPanel.SetDock(topBar, Dock.Top);

            var topStack = new StackPanel { Orientation = Orientation.Horizontal };
            var backBtn = new Button
            {
                Content = "← Vissza",
                Background = Brushes.Transparent,
                Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x9A, 0x1F)),
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Padding = new Thickness(16, 10, 16, 10),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x4D, 0xFF, 0x8C, 0x00)),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand
            };
            backBtn.Click += (s, e) => _mainWindow.NavigateToMain();
            topStack.Children.Add(backBtn);

            topStack.Children.Add(new TextBlock
            {
                Text = "ADMIN TERMINAL",
                Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x9A, 0x1F)),
                FontWeight = FontWeights.Bold,
                FontSize = 18,
                Margin = new Thickness(20, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            });

            var vlBtn = new Button
            {
                Content = "⚠ VIZSGALOCK",
                Background = Brushes.Transparent,
                Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x33, 0x33)),
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Padding = new Thickness(16, 10, 16, 10),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0x33, 0x33)),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand,
                Margin = new Thickness(20, 0, 0, 0)
            };
            vlBtn.Click += (s, e) => ToggleVizsgalock();
            topStack.Children.Add(vlBtn);

            var purgeBtn = new Button
            {
                Content = "⚠ VIZSGAPURGE",
                Background = Brushes.Transparent,
                Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x33, 0x33)),
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Padding = new Thickness(16, 10, 16, 10),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0x33, 0x33)),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand,
                Margin = new Thickness(20, 0, 0, 0)
            };
            purgeBtn.Click += (s, e) => PerformPurge();
            topStack.Children.Add(purgeBtn);

            topBar.Child = topStack;
            dockPanel.Children.Add(topBar);

            TabControl = new TabControl
            {
                Margin = new Thickness(10, 10, 10, 10),
                Background = new SolidColorBrush(Color.FromRgb(0x05, 0x05, 0x05)),
                Foreground = new SolidColorBrush(Color.FromRgb(0xF5, 0xF0, 0xE8)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x29, 0xFF, 0x8C, 0x00))
            };

            TabControl.Items.Add(CreateTab("◧ TERMÉKEK", "ItemsGrid"));
            TabControl.Items.Add(CreateTab("◈ FELHASZNÁLÓK", "UsersGrid"));
            TabControl.Items.Add(CreateTab("📦 RENDELÉSEK", "OrdersGrid"));
            TabControl.Items.Add(CreateTab("⚠ REPORTOK", "ReportsGrid"));

            dockPanel.Children.Add(TabControl);
            Content = dockPanel;
        }

        private TabItem CreateTab(string header, string gridName)
        {
            var tab = new TabItem
            {
                Header = header,
                Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x9A, 0x1F)),
                Background = new SolidColorBrush(Color.FromRgb(0x0F, 0x0F, 0x0F)),
                FontWeight = FontWeights.Bold
            };
            var dataGrid = new DataGrid
            {
                Name = gridName,
                Background = new SolidColorBrush(Color.FromRgb(0x0F, 0x0F, 0x0F)),
                Foreground = new SolidColorBrush(Color.FromRgb(0xF5, 0xF0, 0xE8)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x29, 0xFF, 0x8C, 0x00)),
                BorderThickness = new Thickness(1),
                AutoGenerateColumns = true,
                IsReadOnly = true,
                CanUserAddRows = false,
                CanUserDeleteRows = false,
                SelectionMode = DataGridSelectionMode.Single
            };
            tab.Content = dataGrid;
            return tab;
        }

        private void LoadAdminData()
        {
            try
            {
                using (var conn = _mainWindow.GetConnection())
                {
                    // Termékek
                    var itemsTable = new System.Data.DataTable();
                    using (var cmd = new MySqlCommand(
                        @"SELECT i.id, i.title, u.username AS seller, i.price, 
                                 CASE WHEN i.sold THEN 'Elkelt' ELSE 'Aktív' END AS status,
                                 i.created_at
                          FROM items i JOIN users u ON i.user_id = u.id
                          ORDER BY i.created_at DESC", conn))
                    using (var adapter = new MySql.Data.MySqlClient.MySqlDataAdapter(cmd))
                    {
                        adapter.Fill(itemsTable);
                    }
                    ((DataGrid)((TabItem)TabControl.Items[0]).Content).ItemsSource = itemsTable.DefaultView;

                    // Felhasználók
                    var usersTable = new System.Data.DataTable();
                    using (var cmd = new MySqlCommand(
                        @"SELECT u.id, u.username, u.email,
                                 CASE WHEN a.user_id IS NOT NULL THEN 'Admin' ELSE 'User' END AS role,
                                 COUNT(i.id) AS items,
                                 u.created_at
                          FROM users u
                          LEFT JOIN admins a ON u.id = a.user_id
                          LEFT JOIN items i ON u.id = i.user_id
                          GROUP BY u.id, u.username, u.email, a.user_id, u.created_at
                          ORDER BY u.created_at DESC", conn))
                    using (var adapter = new MySql.Data.MySqlClient.MySqlDataAdapter(cmd))
                    {
                        adapter.Fill(usersTable);
                    }
                    ((DataGrid)((TabItem)TabControl.Items[1]).Content).ItemsSource = usersTable.DefaultView;

                    // Rendelések
                    var ordersTable = new System.Data.DataTable();
                    using (var cmd = new MySqlCommand(
                        @"SELECT o.id, i.title AS item, b.username AS buyer, s.username AS seller,
                                 o.item_price, o.status, o.payment_method, o.created_at
                          FROM orders o
                          JOIN items i ON o.item_id = i.id
                          JOIN users b ON o.buyer_id = b.id
                          JOIN users s ON o.seller_id = s.id
                          ORDER BY o.created_at DESC", conn))
                    using (var adapter = new MySql.Data.MySqlClient.MySqlDataAdapter(cmd))
                    {
                        adapter.Fill(ordersTable);
                    }
                    ((DataGrid)((TabItem)TabControl.Items[2]).Content).ItemsSource = ordersTable.DefaultView;

                    // Riportok
                    try
                    {
                        var reportsTable = new System.Data.DataTable();
                        using (var cmd = new MySqlCommand(
                            @"SELECT r.id, 'item' AS type, r.item_id, i.title, rep.username AS reporter,
                                    own.username AS target, r.reason, r.status, r.created_at
                             FROM reports r
                             JOIN items i ON r.item_id = i.id
                             JOIN users rep ON r.user_id = rep.id
                             JOIN users own ON i.user_id = own.id
                             ORDER BY r.created_at DESC", conn))
                        using (var adapter = new MySql.Data.MySqlClient.MySqlDataAdapter(cmd))
                        {
                            adapter.Fill(reportsTable);
                        }
                        ((DataGrid)((TabItem)TabControl.Items[3]).Content).ItemsSource = reportsTable.DefaultView;
                    }
                    catch
                    {
                        ((DataGrid)((TabItem)TabControl.Items[3]).Content).ItemsSource = null;
                    }
                }
            }
            catch (MySqlException ex)
            {
                MessageBox.Show("Adatbázis hiba: " + ex.Message);
            }
        }

        private void ToggleVizsgalock()
        {
            if (MessageBox.Show("Biztosan átkapcsolod a VIZSGALOCK állapotát?", "Megerősítés",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                try
                {
                    using (var conn = _mainWindow.GetConnection())
                    {
                        bool currentState = _mainWindow.CheckVizsgalock();
                        using (var cmd = new MySqlCommand(
                            "UPDATE vizsgalock_settings SET is_locked = @newState WHERE id = 1", conn))
                        {
                            cmd.Parameters.AddWithValue("@newState", !currentState);
                            cmd.ExecuteNonQuery();
                        }
                        MessageBox.Show($"VIZSGALOCK: {(!currentState ? "BEKAPCSOLVA" : "KIKAPCSOLVA")}",
                            "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Hiba: " + ex.Message);
                }
            }
        }

        private void PerformPurge()
        {
            if (MessageBox.Show(
                "Ez véglegesen TÖRLI az összes nem-admin felhasználót (kivéve: gabi, martin, cuci, admin)!\nBiztosan folytatod?",
                "VIZSGAPURGE", MessageBoxButton.YesNo, MessageBoxImage.Error) == MessageBoxResult.Yes)
            {
                try
                {
                    using (var conn = _mainWindow.GetConnection())
                    using (var transaction = conn.BeginTransaction())
                    {
                        try
                        {
                            string[] keepers = { "gabi", "martin", "cuci", "admin" };
                            var keeperParams = string.Join(",", keepers.Select(k => $"'{k}'"));
                            using (var cmd = new MySqlCommand(
                                $"SELECT id FROM users WHERE LOWER(username) NOT IN ({keeperParams})",
                                conn, transaction))
                            using (var reader = cmd.ExecuteReader())
                            {
                                var idsToDelete = new List<int>();
                                while (reader.Read()) idsToDelete.Add(reader.GetInt32("id"));
                                reader.Close();

                                if (idsToDelete.Count > 0)
                                {
                                    var idList = string.Join(",", idsToDelete);
                                    using (var delCmd = new MySqlCommand(
                                        $"DELETE FROM users WHERE id IN ({idList})", conn, transaction))
                                    {
                                        delCmd.ExecuteNonQuery();
                                    }
                                }
                            }
                            transaction.Commit();
                            MessageBox.Show("Purge sikeres!", "Kész", MessageBoxButton.OK, MessageBoxImage.Information);
                            LoadAdminData();
                        }
                        catch
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Hiba: " + ex.Message);
                }
            }
        }
    }

    // Üzenetek oldal
    public class MessagesPage : Page
    {
        private readonly MainWindow _mainWindow;
        private ListBox PartnersList;
        private ListBox MessagesList;
        private TextBox MessageInput;
        private int? selectedPartnerId;
        private List<PartnerViewModel> partners = new List<PartnerViewModel>();
        private ObservableCollection<MessageViewModel> messages = new ObservableCollection<MessageViewModel>();

        public MessagesPage(MainWindow mainWindow)
        {
            _mainWindow = mainWindow;
            InitializeUI();
            LoadPartners();
        }

        private void InitializeUI()
        {
            Background = new SolidColorBrush(Color.FromRgb(0x05, 0x05, 0x05));
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(250) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(5) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Partnerek lista
            var partnersStack = new StackPanel();
            var partnersHeader = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x0A, 0x0A, 0x0A)),
                Padding = new Thickness(10, 10, 10, 10)
            };
            partnersHeader.Child = new TextBlock
            {
                Text = "Beszélgetések",
                Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x9A, 0x1F)),
                FontWeight = FontWeights.Bold
            };
            partnersStack.Children.Add(partnersHeader);

            PartnersList = new ListBox
            {
                Background = new SolidColorBrush(Color.FromRgb(0x05, 0x05, 0x05)),
                Foreground = new SolidColorBrush(Color.FromRgb(0xF5, 0xF0, 0xE8)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x29, 0xFF, 0x8C, 0x00)),
                BorderThickness = new Thickness(1),
                DisplayMemberPath = "Username"
            };
            PartnersList.SelectionChanged += (s, e) =>
            {
                if (PartnersList.SelectedItem is PartnerViewModel p)
                {
                    selectedPartnerId = p.Id;
                    LoadMessages(p.Id);
                }
            };
            partnersStack.Children.Add(PartnersList);
            Grid.SetColumn(partnersStack, 0);

            // Elválasztó
            var separator = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0x29, 0xFF, 0x8C, 0x00)),
                Width = 1
            };
            Grid.SetColumn(separator, 1);

            // Üzenetek
            var messagesStack = new StackPanel();
            var messagesHeader = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x0A, 0x0A, 0x0A)),
                Padding = new Thickness(10, 10, 10, 10)
            };
            messagesHeader.Child = new TextBlock
            {
                Text = "Üzenetek",
                Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x9A, 0x1F)),
                FontWeight = FontWeights.Bold
            };
            messagesStack.Children.Add(messagesHeader);

            MessagesList = new ListBox
            {
                Background = new SolidColorBrush(Color.FromRgb(0x05, 0x05, 0x05)),
                Foreground = new SolidColorBrush(Color.FromRgb(0xF5, 0xF0, 0xE8)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x29, 0xFF, 0x8C, 0x00)),
                BorderThickness = new Thickness(1),
                DisplayMemberPath = "Text"
            };
            messagesStack.Children.Add(MessagesList);

            // Input
            var inputBar = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x0A, 0x0A, 0x0A)),
                Padding = new Thickness(10, 10, 10, 10)
            };
            var inputGrid = new Grid();
            inputGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            inputGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            MessageInput = new TextBox
            {
                Background = new SolidColorBrush(Color.FromRgb(0x2D, 0x24, 0x18)),
                Foreground = new SolidColorBrush(Color.FromRgb(0xF5, 0xF0, 0xE8)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x3D, 0x35, 0x28)),
                BorderThickness = new Thickness(1),
                FontSize = 14,
                Padding = new Thickness(14, 12, 14, 12)
            };
            Grid.SetColumn(MessageInput, 0);

            var sendBtn = new Button
            {
                Content = "➤",
                FontSize = 15,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(0x0A, 0x05, 0x00)),
                Padding = new Thickness(0, 10, 0, 10),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                Width = 44,
                Height = 44,
                Margin = new Thickness(10, 0, 0, 0)
            };
            sendBtn.Background = new LinearGradientBrush(
                Color.FromRgb(0xFF, 0xAB, 0x35),
                Color.FromRgb(0xB3, 0x55, 0x00), 90);
            sendBtn.Click += (s, e) => SendMessage();
            Grid.SetColumn(sendBtn, 1);

            inputGrid.Children.Add(MessageInput);
            inputGrid.Children.Add(sendBtn);
            inputBar.Child = inputGrid;
            messagesStack.Children.Add(inputBar);

            Grid.SetColumn(messagesStack, 2);

            grid.Children.Add(partnersStack);
            grid.Children.Add(separator);
            grid.Children.Add(messagesStack);
            Content = grid;
        }

        private void LoadPartners()
        {
            partners.Clear();
            try
            {
                using (var conn = _mainWindow.GetConnection())
                using (var cmd = new MySqlCommand(
                    @"SELECT u.id, u.username,
                             MAX(m.sent_at) AS last_msg,
                             SUM(CASE WHEN m.receiver_id = @me AND m.is_read = 0 THEN 1 ELSE 0 END) AS unread
                      FROM users u
                      JOIN uzenetek m ON (
                          (m.sender_id = u.id AND m.receiver_id = @me2)
                          OR (m.receiver_id = u.id AND m.sender_id = @me3)
                      )
                      LEFT JOIN hidden_conversations hc ON hc.user_id = @me4 AND hc.partner_id = u.id
                      WHERE u.id != @me5 AND hc.user_id IS NULL
                      GROUP BY u.id, u.username
                      ORDER BY last_msg DESC", conn))
                {
                    cmd.Parameters.AddWithValue("@me", MainWindow.LoggedInUserId);
                    cmd.Parameters.AddWithValue("@me2", MainWindow.LoggedInUserId);
                    cmd.Parameters.AddWithValue("@me3", MainWindow.LoggedInUserId);
                    cmd.Parameters.AddWithValue("@me4", MainWindow.LoggedInUserId);
                    cmd.Parameters.AddWithValue("@me5", MainWindow.LoggedInUserId);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            partners.Add(new PartnerViewModel
                            {
                                Id = reader.GetInt32("id"),
                                Username = reader.GetString("username"),
                                UnreadCount = reader.GetInt32("unread"),
                                LastMessageAt = reader.IsDBNull(reader.GetOrdinal("last_msg"))
                                    ? DateTime.MinValue
                                    : reader.GetDateTime("last_msg")
                            });
                        }
                    }
                }
            }
            catch { }
            PartnersList.ItemsSource = partners;
        }

        private void LoadMessages(int partnerId)
        {
            messages.Clear();
            try
            {
                using (var conn = _mainWindow.GetConnection())
                {
                    // Olvasottá jelölés
                    using (var markCmd = new MySqlCommand(
                        "UPDATE uzenetek SET is_read = 1 WHERE sender_id = @pid AND receiver_id = @me AND is_read = 0",
                        conn))
                    {
                        markCmd.Parameters.AddWithValue("@pid", partnerId);
                        markCmd.Parameters.AddWithValue("@me", MainWindow.LoggedInUserId);
                        markCmd.ExecuteNonQuery();
                    }

                    using (var cmd = new MySqlCommand(
                        @"SELECT id, sender_id, receiver_id, message, sent_at, is_read
                          FROM uzenetek
                          WHERE (sender_id = @me AND receiver_id = @pid)
                             OR (sender_id = @pid AND receiver_id = @me2)
                          ORDER BY sent_at ASC", conn))
                    {
                        cmd.Parameters.AddWithValue("@me", MainWindow.LoggedInUserId);
                        cmd.Parameters.AddWithValue("@pid", partnerId);
                        cmd.Parameters.AddWithValue("@me2", MainWindow.LoggedInUserId);
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                messages.Add(new MessageViewModel
                                {
                                    Id = reader.GetString("id"),
                                    SenderId = reader.GetInt32("sender_id"),
                                    ReceiverId = reader.GetInt32("receiver_id"),
                                    Text = reader.GetString("message"),
                                    SentAt = reader.GetDateTime("sent_at"),
                                    IsRead = reader.GetBoolean("is_read"),
                                    IsOwn = reader.GetInt32("sender_id") == MainWindow.LoggedInUserId
                                });
                            }
                        }
                    }
                }
            }
            catch { }
            MessagesList.ItemsSource = messages;
        }

        private void SendMessage()
        {
            if (selectedPartnerId.HasValue && !string.IsNullOrWhiteSpace(MessageInput.Text))
            {
                try
                {
                    string msgId = _mainWindow.GenerateId(25);
                    using (var conn = _mainWindow.GetConnection())
                    using (var cmd = new MySqlCommand(
                        "INSERT INTO uzenetek (id, sender_id, receiver_id, message) VALUES (@id, @sid, @rid, @msg)",
                        conn))
                    {
                        cmd.Parameters.AddWithValue("@id", msgId);
                        cmd.Parameters.AddWithValue("@sid", MainWindow.LoggedInUserId);
                        cmd.Parameters.AddWithValue("@rid", selectedPartnerId.Value);
                        cmd.Parameters.AddWithValue("@msg", MessageInput.Text);
                        cmd.ExecuteNonQuery();
                    }
                    MessageInput.Clear();
                    LoadMessages(selectedPartnerId.Value);
                    LoadPartners();
                }
                catch (MySqlException ex)
                {
                    MessageBox.Show("Hiba: " + ex.Message);
                }
            }
        }
    }

    // Vásárlás oldal
    public class PurchasePage : Page
    {
        private readonly MainWindow _mainWindow;
        private readonly string _itemId;
        private ItemViewModel _item;

        public PurchasePage(MainWindow mainWindow, string itemId)
        {
            _mainWindow = mainWindow;
            _itemId = itemId;
            InitializeUI();
            LoadItemData();
        }

        private void InitializeUI()
        {
            Background = new SolidColorBrush(Color.FromRgb(0x05, 0x05, 0x05));
            var scroll = new ScrollViewer();
            var stack = new StackPanel { Margin = new Thickness(40, 40, 40, 40) };

            var backBtn = new Button
            {
                Content = "← Vissza",
                Background = Brushes.Transparent,
                Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x9A, 0x1F)),
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Padding = new Thickness(16, 10, 16, 10),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x4D, 0xFF, 0x8C, 0x00)),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            backBtn.Click += (s, e) => _mainWindow.NavigateToMain();
            stack.Children.Add(backBtn);

            stack.Children.Add(new TextBlock
            {
                Text = "🛒 Vásárlás",
                FontSize = 22,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x9A, 0x1F)),
                Margin = new Thickness(0, 16, 0, 16)
            });

            // Szállítási adatok űrlap
            var formGrid = new Grid();
            formGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            formGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            formGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            formGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            formGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            formGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            formGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            formGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var nameBox = CreateFormField("Teljes név *", 0, 0, true);
            var emailBox = CreateFormField("Email *", 0, 1);
            var phoneBox = CreateFormField("Telefonszám *", 1, 1);
            var zipBox = CreateFormField("Irányítószám *", 0, 2);
            var cityBox = CreateFormField("Város *", 1, 2);
            var addressBox = CreateFormField("Cím *", 0, 3, true);

            var submitBtn = new Button
            {
                Content = "Rendelés leadása",
                FontSize = 15,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(0x0A, 0x05, 0x00)),
                Padding = new Thickness(0, 14, 0, 14),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                Margin = new Thickness(0, 20, 0, 0)
            };
            submitBtn.Background = new LinearGradientBrush(
                Color.FromRgb(0xFF, 0xAB, 0x35),
                Color.FromRgb(0xB3, 0x55, 0x00), 90);
            submitBtn.Click += (s, e) => SubmitOrder(
                ((TextBox)((StackPanel)nameBox).Children[1]).Text,
                ((TextBox)((StackPanel)emailBox).Children[1]).Text,
                ((TextBox)((StackPanel)phoneBox).Children[1]).Text,
                ((TextBox)((StackPanel)zipBox).Children[1]).Text,
                ((TextBox)((StackPanel)cityBox).Children[1]).Text,
                ((TextBox)((StackPanel)addressBox).Children[1]).Text);

            Grid.SetRow(submitBtn, 5);
            Grid.SetColumnSpan(submitBtn, 2);
            formGrid.Children.Add(submitBtn);

            stack.Children.Add(formGrid);
            scroll.Content = stack;
            Content = scroll;
        }

        private UIElement CreateFormField(string label, int row, int col, bool fullWidth = false)
        {
            var panel = new StackPanel { Margin = new Thickness(5, 5, 5, 5) };
            panel.Children.Add(new TextBlock
            {
                Text = label,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x9A, 0x1F)),
                Margin = new Thickness(0, 0, 0, 6)
            });
            panel.Children.Add(new TextBox
            {
                Background = new SolidColorBrush(Color.FromRgb(0x2D, 0x24, 0x18)),
                Foreground = new SolidColorBrush(Color.FromRgb(0xF5, 0xF0, 0xE8)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x3D, 0x35, 0x28)),
                BorderThickness = new Thickness(1),
                FontSize = 14,
                Padding = new Thickness(14, 12, 14, 12)
            });
            Grid.SetRow(panel, row);
            Grid.SetColumn(panel, col);
            if (fullWidth) Grid.SetColumnSpan(panel, 2);
            return panel;
        }

        private void LoadItemData()
        {
            try
            {
                using (var conn = _mainWindow.GetConnection())
                using (var cmd = new MySqlCommand(
                    @"SELECT i.id, i.title, i.price, i.description, i.sold, u.username AS seller
                      FROM items i JOIN users u ON i.user_id = u.id
                      WHERE i.id = @id", conn))
                {
                    cmd.Parameters.AddWithValue("@id", _itemId);
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            _item = new ItemViewModel
                            {
                                Id = reader.GetString("id"),
                                Title = reader.GetString("title"),
                                Price = reader.GetDecimal("price"),
                                Description = reader.GetString("description"),
                                IsSold = reader.GetBoolean("sold"),
                                SellerName = reader.GetString("seller")
                            };
                        }
                    }
                }
            }
            catch { }
        }

        private void SubmitOrder(string name, string email, string phone, string zip, string city, string address)
        {
            if (!_mainWindow.CanPerformWriteOperation())
            {
                MessageBox.Show("VIZSGALOCK aktív!", "Hiba", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(phone) || string.IsNullOrWhiteSpace(zip) ||
                string.IsNullOrWhiteSpace(city) || string.IsNullOrWhiteSpace(address))
            {
                MessageBox.Show("Minden mező kitöltése kötelező!");
                return;
            }

            try
            {
                using (var conn = _mainWindow.GetConnection())
                using (var transaction = conn.BeginTransaction())
                {
                    try
                    {
                        string orderId = _mainWindow.GenerateId();
                        using (var cmd = new MySqlCommand(
                            @"INSERT INTO orders (id, buyer_id, seller_id, item_id, status,
                                shipping_name, shipping_email, shipping_phone,
                                shipping_zip, shipping_city, shipping_address,
                                payment_method)
                              VALUES (@id, @bid, (SELECT user_id FROM items WHERE id = @iid), @iid, 'pending',
                                @sname, @semail, @sphone, @szip, @scity, @saddr, 'cod')",
                            conn, transaction))
                        {
                            cmd.Parameters.AddWithValue("@id", orderId);
                            cmd.Parameters.AddWithValue("@bid", MainWindow.LoggedInUserId);
                            cmd.Parameters.AddWithValue("@iid", _itemId);
                            cmd.Parameters.AddWithValue("@sname", name);
                            cmd.Parameters.AddWithValue("@semail", email);
                            cmd.Parameters.AddWithValue("@sphone", phone);
                            cmd.Parameters.AddWithValue("@szip", zip);
                            cmd.Parameters.AddWithValue("@scity", city);
                            cmd.Parameters.AddWithValue("@saddr", address);
                            cmd.ExecuteNonQuery();
                        }

                        using (var soldCmd = new MySqlCommand(
                            "UPDATE items SET sold = 1 WHERE id = @iid", conn, transaction))
                        {
                            soldCmd.Parameters.AddWithValue("@iid", _itemId);
                            soldCmd.ExecuteNonQuery();
                        }

                        transaction.Commit();
                        MessageBox.Show("Rendelés sikeresen rögzítve!", "Siker",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                        _mainWindow.NavigateToMain();
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Hiba: " + ex.Message);
            }
        }
    }
}