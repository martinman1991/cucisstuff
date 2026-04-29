using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace cucisstuff
{
    public partial class MainWindow : Window
    {
        private const string API_URL = "http://cuci.local.pepa.hu/api.php";
        private const string API_TOKEN = "AzEnTitkosTokenem2024_CucisStuff!XyZ";

        private static readonly HttpClient _httpClient = new HttpClient();

        public static int? LoggedInUserId { get; set; }
        public static string LoggedInUsername { get; set; }
        public static bool IsAdmin { get; set; }

        public MainWindow()
        {
            System.Net.ServicePointManager.SecurityProtocol =
                System.Net.SecurityProtocolType.Tls12;

            InitializeComponent();
            MainFrame.Navigate(new LoginPage(this));
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
                DragMove();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

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

        // ============================================================
        // API SEGÉDFÜGGVÉNYEK
        // ============================================================
        public async Task<JsonElement> ApiCallAsync(string query, object[] parameters = null, string type = "select")
        {
            var payload = new
            {
                query = query,
                @params = parameters ?? Array.Empty<object>(),
                type = type
            };

            var jsonPayload = JsonSerializer.Serialize(payload);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            var request = new HttpRequestMessage(HttpMethod.Post, API_URL);
            request.Headers.Add("X-Api-Token", API_TOKEN);
            request.Content = content;

            var response = await _httpClient.SendAsync(request);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"API hiba: {response.StatusCode} - {responseBody}");
            }

            JsonElement root;
            using (var doc = JsonDocument.Parse(responseBody))
            {
                root = doc.RootElement.Clone();

                if (root.TryGetProperty("error", out var error))
                {
                    throw new Exception($"API hiba: {error.GetString()}");
                }
            }
            return root;
        }

        public async Task<List<T>> ApiSelectAsync<T>(string query, object[] parameters = null)
        {
            var result = await ApiCallAsync(query, parameters, "select");
            var data = result.GetProperty("data");
            return JsonSerializer.Deserialize<List<T>>(data.GetRawText()) ?? new List<T>();
        }

        public async Task<T> ApiScalarAsync<T>(string query, object[] parameters = null)
        {
            var result = await ApiCallAsync(query, parameters, "scalar");
            var data = result.GetProperty("data");

            if (data.ValueKind == JsonValueKind.Null)
                return default;

            return JsonSerializer.Deserialize<T>(data.GetRawText());
        }

        public async Task<long> ApiInsertAsync(string query, object[] parameters = null)
        {
            var result = await ApiCallAsync(query, parameters, "insert");
            return result.GetProperty("lastId").GetInt64();
        }

        public async Task<int> ApiExecuteAsync(string query, object[] parameters = null, string type = "update")
        {
            var result = await ApiCallAsync(query, parameters, type);
            return result.GetProperty("affected").GetInt32();
        }

        public async Task<bool> CheckVizsgalockAsync()
        {
            try
            {
                var result = await ApiScalarAsync<string>(
                    "SELECT is_locked FROM vizsgalock_settings WHERE id = 1"
                );
                return result == "1";
            }
            catch { return false; }
        }

        public bool CheckVizsgalock()
        {
            try
            {
                return Task.Run(() => CheckVizsgalockAsync()).GetAwaiter().GetResult();
            }
            catch { return false; }
        }

        public async Task<bool> IsVizsgalockExceptedAsync(int userId)
        {
            try
            {
                var adminCount = await ApiScalarAsync<string>(
                    "SELECT COUNT(*) FROM admins WHERE user_id = @uid",
                    new object[] { userId }
                );
                if (adminCount != "0") return true;

                var exceptCount = await ApiScalarAsync<string>(
                    "SELECT COUNT(*) FROM vizsgalock_exceptions WHERE user_id = @uid",
                    new object[] { userId }
                );
                return exceptCount != "0";
            }
            catch { return false; }
        }

        public bool IsVizsgalockExcepted(int userId)
        {
            try
            {
                return Task.Run(() => IsVizsgalockExceptedAsync(userId)).GetAwaiter().GetResult();
            }
            catch { return false; }
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

        public string BCryptHash(string password)
        {
            return BCrypt.Net.BCrypt.HashPassword(password, BCrypt.Net.BCrypt.GenerateSalt(12));
        }

        public bool BCryptVerify(string password, string hash)
        {
            try { return BCrypt.Net.BCrypt.Verify(password, hash); }
            catch { return false; }
        }

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
                    bitmap.Freeze();

                    int srcWidth = bitmap.PixelWidth;
                    int srcHeight = bitmap.PixelHeight;
                    if (srcWidth <= maxDim && srcHeight <= maxDim) return imageData;

                    double ratio = (double)srcWidth / srcHeight;
                    int newWidth, newHeight;
                    if (srcWidth > srcHeight)
                    { newWidth = maxDim; newHeight = (int)Math.Round(maxDim / ratio); }
                    else
                    { newHeight = maxDim; newWidth = (int)Math.Round(maxDim * ratio); }

                    var resizedBitmap = new TransformedBitmap(bitmap,
                        new ScaleTransform((double)newWidth / srcWidth, (double)newHeight / srcHeight));

                    var encoder = new JpegBitmapEncoder { QualityLevel = 85 };
                    encoder.Frames.Add(BitmapFrame.Create(resizedBitmap));
                    using (var outputMs = new MemoryStream())
                    {
                        encoder.Save(outputMs);
                        return outputMs.ToArray();
                    }
                }
            }
            catch { return imageData; }
        }

        public static Button MakeOrangeButton(string content, double fontSize = 14)
        {
            var btn = new Button
            {
                Content = content,
                FontSize = fontSize,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(0x0A, 0x05, 0x00)),
                Padding = new Thickness(16, 12, 16, 12),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };
            btn.Background = new LinearGradientBrush(
                Color.FromRgb(0xFF, 0xAB, 0x35),
                Color.FromRgb(0xB3, 0x55, 0x00), 90);
            ApplyRoundedTemplate(btn, new CornerRadius(12));
            return btn;
        }

        public static Button MakeGhostButton(string content, double fontSize = 14)
        {
            var btn = new Button
            {
                Content = content,
                FontSize = fontSize,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x9A, 0x1F)),
                Background = Brushes.Transparent,
                Padding = new Thickness(16, 10, 16, 10),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x4D, 0xFF, 0x8C, 0x00)),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand
            };
            ApplyRoundedTemplate(btn, new CornerRadius(12));
            return btn;
        }

        public static Button MakeRedGhostButton(string content, double fontSize = 14)
        {
            var btn = new Button
            {
                Content = content,
                FontSize = fontSize,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x44, 0x44)),
                Background = Brushes.Transparent,
                Padding = new Thickness(16, 10, 16, 10),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x4D, 0xFF, 0x44, 0x44)),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand
            };
            ApplyRoundedTemplate(btn, new CornerRadius(12));
            return btn;
        }

        private static void ApplyRoundedTemplate(Button btn, CornerRadius cr)
        {
            var t = new ControlTemplate(typeof(Button));
            var bdrFef = new FrameworkElementFactory(typeof(Border));
            bdrFef.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
            bdrFef.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Button.BorderBrushProperty));
            bdrFef.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Button.BorderThicknessProperty));
            bdrFef.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Button.PaddingProperty));
            bdrFef.SetValue(Border.CornerRadiusProperty, cr);
            bdrFef.Name = "bd";
            var cpFef = new FrameworkElementFactory(typeof(ContentPresenter));
            cpFef.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            cpFef.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            bdrFef.AppendChild(cpFef);
            t.VisualTree = bdrFef;
            btn.Template = t;
        }

        public static TextBox MakeInput(string placeholder = "")
        {
            return new TextBox
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

        public static TextBlock MakeLabel(string text)
        {
            return new TextBlock
            {
                Text = text,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x9A, 0x1F)),
                Margin = new Thickness(0, 0, 0, 6)
            };
        }

        // ============================================
        // EGYSZERŰ INPUT DIALOG
        // ============================================
        public static string ShowInputDialog(string prompt, string title, string defaultValue = "")
        {
            var dialog = new Window
            {
                Title = title,
                Width = 400,
                Height = 180,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                WindowStyle = WindowStyle.ToolWindow,
                ResizeMode = ResizeMode.NoResize,
                Owner = Application.Current.MainWindow
            };

            var grid = new Grid { Margin = new Thickness(10) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var label = new TextBlock
            {
                Text = prompt,
                Margin = new Thickness(0, 5, 0, 8),
                FontSize = 13,
                Foreground = new SolidColorBrush(Color.FromRgb(0xF5, 0xF0, 0xE8))
            };
            Grid.SetRow(label, 0);
            grid.Children.Add(label);

            var textBox = new TextBox
            {
                Text = defaultValue,
                FontSize = 14,
                Padding = new Thickness(8, 6, 8, 6),
                Background = new SolidColorBrush(Color.FromRgb(0x2D, 0x24, 0x18)),
                Foreground = new SolidColorBrush(Color.FromRgb(0xF5, 0xF0, 0xE8)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x3D, 0x35, 0x28)),
                CaretBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0x9A, 0x1F))
            };
            textBox.SelectAll();
            textBox.Focus();
            Grid.SetRow(textBox, 1);
            grid.Children.Add(textBox);

            var btnPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 12, 0, 0)
            };
            var okBtn = MainWindow.MakeOrangeButton("OK", 13);
            okBtn.Width = 80;
            okBtn.Height = 32;
            okBtn.Click += (s, e) => { dialog.Tag = textBox.Text; dialog.DialogResult = true; dialog.Close(); };
            var cancelBtn = MainWindow.MakeGhostButton("Mégse", 13);
            cancelBtn.Width = 80;
            cancelBtn.Height = 32;
            cancelBtn.Margin = new Thickness(8, 0, 0, 0);
            cancelBtn.Click += (s, e) => { dialog.DialogResult = false; dialog.Close(); };
            btnPanel.Children.Add(cancelBtn);
            btnPanel.Children.Add(okBtn);
            Grid.SetRow(btnPanel, 2);
            grid.Children.Add(btnPanel);

            dialog.Content = grid;
            dialog.Background = new SolidColorBrush(Color.FromRgb(0x0F, 0x0F, 0x0F));

            textBox.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Return) { dialog.Tag = textBox.Text; dialog.DialogResult = true; dialog.Close(); }
                if (e.Key == Key.Escape) { dialog.DialogResult = false; dialog.Close(); }
            };

            dialog.Loaded += (s, e) => textBox.Focus();

            bool? result = dialog.ShowDialog();
            if (result == true)
                return dialog.Tag?.ToString() ?? "";
            return null;
        }
    }

    // ============================================
    // VIEWMODEL ALAPOSZTÁLYOK
    // ============================================
    public class ViewModelBase : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

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
        public Brush StatusColor => IsSold
            ? new SolidColorBrush(Color.FromRgb(0xFF, 0x44, 0x44))
            : new SolidColorBrush(Color.FromRgb(0x00, 0xC8, 0x51));
    }

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
    }

    public class PartnerViewModel : ViewModelBase
    {
        public int Id { get; set; }
        public string Username { get; set; }
        public string ProfilePicture { get; set; }
        public string AvatarInitial => string.IsNullOrEmpty(Username) ? "?" : Username[0].ToString().ToUpper();
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
    }

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

    // ============================================
    // LOGIN OLDAL
    // ============================================
    public class LoginPage : Page
    {
        private readonly MainWindow _mw;
        private TextBox _loginUser;
        private PasswordBox _loginPass;
        private TextBox _regUser;
        private TextBox _regEmail;
        private PasswordBox _regPass;
        private PasswordBox _regPass2;
        private StackPanel _loginPanel;
        private StackPanel _regPanel;
        private TextBlock _subtitle;
        private Border _errBorder;
        private TextBlock _errText;

        public LoginPage(MainWindow mw)
        {
            _mw = mw;
            Build();
        }

        private void Build()
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

            var stack = new StackPanel();
            card.Child = stack;

            stack.Children.Add(new TextBlock
            {
                Text = "Cuci's Stuff",
                FontSize = 28,
                FontWeight = FontWeights.Light,
                Foreground = new SolidColorBrush(Color.FromRgb(0xF5, 0xF0, 0xE8)),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 6)
            });

            _subtitle = new TextBlock
            {
                Text = "Bejelentkezés",
                FontSize = 14,
                Foreground = new SolidColorBrush(Color.FromRgb(0x8A, 0x7A, 0x65)),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 28)
            };
            stack.Children.Add(_subtitle);

            _errBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0x26, 0xFF, 0x32, 0x32)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x4D, 0xFF, 0x4D, 0x4D)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(12, 10, 12, 10),
                Margin = new Thickness(0, 0, 0, 16),
                Visibility = Visibility.Collapsed
            };
            _errText = new TextBlock
            {
                Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x80, 0x80)),
                FontSize = 13,
                TextWrapping = TextWrapping.Wrap,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            _errBorder.Child = _errText;
            stack.Children.Add(_errBorder);

            _loginPanel = new StackPanel();

            _loginUser = MakePlaceholderBox("Felhasználónév vagy email");
            _loginPanel.Children.Add(_loginUser);
            _loginPanel.Children.Add(new FrameworkElement { Height = 12 });

            _loginPass = MakePwdBox();
            _loginPanel.Children.Add(_loginPass);
            _loginPanel.Children.Add(new FrameworkElement { Height = 20 });

            var loginBtn = MainWindow.MakeOrangeButton("Bejelentkezés", 15);
            loginBtn.Margin = new Thickness(0, 0, 0, 16);
            loginBtn.Click += LoginBtn_Click;
            _loginPanel.Children.Add(loginBtn);

            _loginPanel.Children.Add(MakeSeparator());

            var toRegBtn = MainWindow.MakeGhostButton("Regisztráció");
            toRegBtn.Click += (s, e) => SwitchToReg();
            _loginPanel.Children.Add(toRegBtn);

            stack.Children.Add(_loginPanel);

            _regPanel = new StackPanel { Visibility = Visibility.Collapsed };

            _regUser = MakePlaceholderBox("Felhasználónév");
            _regPanel.Children.Add(_regUser);
            _regPanel.Children.Add(new FrameworkElement { Height = 12 });

            _regEmail = MakePlaceholderBox("Email");
            _regPanel.Children.Add(_regEmail);
            _regPanel.Children.Add(new FrameworkElement { Height = 12 });

            _regPass = MakePwdBox();
            _regPanel.Children.Add(_regPass);
            _regPanel.Children.Add(new FrameworkElement { Height = 12 });

            _regPass2 = MakePwdBox();
            _regPanel.Children.Add(_regPass2);
            _regPanel.Children.Add(new FrameworkElement { Height = 20 });

            var regBtn = MainWindow.MakeOrangeButton("Regisztráció", 15);
            regBtn.Margin = new Thickness(0, 0, 0, 16);
            regBtn.Click += RegBtn_Click;
            _regPanel.Children.Add(regBtn);

            var backBtn = MainWindow.MakeGhostButton("← Vissza a bejelentkezéshez");
            backBtn.Click += (s, e) => SwitchToLogin();
            _regPanel.Children.Add(backBtn);

            stack.Children.Add(_regPanel);
            outerGrid.Children.Add(card);
            Content = outerGrid;
        }

        private TextBox MakePlaceholderBox(string placeholder)
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
                CaretBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0x9A, 0x1F)),
                Tag = placeholder
            };
            tb.GotFocus += (s, e) =>
            {
                if (tb.Text == (string)tb.Tag)
                {
                    tb.Text = "";
                    tb.Foreground = new SolidColorBrush(Color.FromRgb(0xF5, 0xF0, 0xE8));
                }
            };
            tb.LostFocus += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(tb.Text))
                {
                    tb.Text = (string)tb.Tag;
                    tb.Foreground = new SolidColorBrush(Color.FromRgb(0x8A, 0x7A, 0x65));
                }
            };
            return tb;
        }

        private PasswordBox MakePwdBox()
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

        private UIElement MakeSeparator()
        {
            var g = new Grid { Margin = new Thickness(0, 6, 0, 10) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var left = new Border { Height = 1, Background = new SolidColorBrush(Color.FromArgb(0x33, 0xFF, 0x8C, 0x00)), VerticalAlignment = VerticalAlignment.Center };
            var mid = new TextBlock { Text = "VAGY", Foreground = new SolidColorBrush(Color.FromArgb(0x80, 0xFF, 0x8C, 0x00)), FontSize = 11, Margin = new Thickness(14, 0, 14, 0) };
            var right = new Border { Height = 1, Background = new SolidColorBrush(Color.FromArgb(0x33, 0xFF, 0x8C, 0x00)), VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(left, 0); Grid.SetColumn(mid, 1); Grid.SetColumn(right, 2);
            g.Children.Add(left); g.Children.Add(mid); g.Children.Add(right);
            return g;
        }

        private void ShowError(string msg)
        {
            _errText.Text = msg;
            _errBorder.Visibility = Visibility.Visible;
            var t = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            t.Tick += (s, e) => { _errBorder.Visibility = Visibility.Collapsed; ((System.Windows.Threading.DispatcherTimer)s).Stop(); };
            t.Start();
        }

        private void SwitchToReg()
        {
            _loginPanel.Visibility = Visibility.Collapsed;
            _regPanel.Visibility = Visibility.Visible;
            _subtitle.Text = "Regisztráció";
            _errBorder.Visibility = Visibility.Collapsed;
        }

        private void SwitchToLogin()
        {
            _regPanel.Visibility = Visibility.Collapsed;
            _loginPanel.Visibility = Visibility.Visible;
            _subtitle.Text = "Bejelentkezés";
            _errBorder.Visibility = Visibility.Collapsed;
        }

        private async void LoginBtn_Click(object sender, RoutedEventArgs e)
        {
            string user = _loginUser.Text.Trim();
            string pass = _loginPass.Password;

            if (string.IsNullOrWhiteSpace(user) || user == "Felhasználónév vagy email")
            { ShowError("Add meg a felhasználónevet vagy email-t!"); return; }
            if (string.IsNullOrWhiteSpace(pass))
            { ShowError("Add meg a jelszót!"); return; }

            try
            {
                var result = await _mw.ApiCallAsync(
                    @"SELECT u.id, u.username, p.password_hash
                    FROM users u
                    JOIN passwords p ON u.password_id = p.id
                    WHERE u.email = @p0 OR u.username = @p1
                    LIMIT 1",
                    new object[] { user, user },
                    "select"
                );

                var data = result.GetProperty("data");

                if (data.GetArrayLength() > 0)
                {
                    var row = data[0];
                    string hash = row.GetProperty("password_hash").GetString();
                    int uid = row.GetProperty("id").GetInt32();
                    string uname = row.GetProperty("username").GetString();

                    if (_mw.BCryptVerify(pass, hash))
                    {
                        MainWindow.LoggedInUserId = uid;
                        MainWindow.LoggedInUsername = uname;

                        var adminResult = await _mw.ApiScalarAsync<int>(
                            "SELECT COUNT(*) FROM admins WHERE user_id = @p0",
                            new object[] { uid }
                        );
                        MainWindow.IsAdmin = adminResult > 0;

                        _mw.NavigateToMain();
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
            catch (Exception ex)
            {
                ShowError("Hiba: " + ex.Message + " | " + ex.InnerException?.Message);
            }
        }

        private async void RegBtn_Click(object sender, RoutedEventArgs e)
        {
            string uname = _regUser.Text.Trim();
            string email = _regEmail.Text.Trim();
            string pass = _regPass.Password;
            string pass2 = _regPass2.Password;

            if (string.IsNullOrWhiteSpace(uname) || uname == "Felhasználónév")
            { ShowError("Add meg a felhasználónevet!"); return; }
            if (string.IsNullOrWhiteSpace(email) || email == "Email" || !email.Contains("@"))
            { ShowError("Érvénytelen email cím!"); return; }
            if (string.IsNullOrWhiteSpace(pass))
            { ShowError("Add meg a jelszót!"); return; }
            if (pass != pass2)
            { ShowError("A jelszavak nem egyeznek!"); return; }
            if (pass.Length < 6)
            { ShowError("A jelszónak legalább 6 karakter kell!"); return; }
            if (await _mw.CheckVizsgalockAsync())
            { ShowError("Regisztráció most nem lehetséges (VIZSGALOCK aktív)."); return; }

            try
            {
                var checkResult = await _mw.ApiCallAsync(
                    "SELECT email, username FROM users WHERE email = @p0 OR username = @p1 LIMIT 1",
                    new object[] { email, uname },
                    "select"
                );

                var checkData = checkResult.GetProperty("data");
                if (checkData.GetArrayLength() > 0)
                {
                    var existingRow = checkData[0];
                    string existingEmail = existingRow.GetProperty("email").GetString();
                    ShowError(existingEmail == email
                        ? "Ez az email már foglalt!"
                        : "Ez a felhasználónév már foglalt!");
                    return;
                }

                string hash = _mw.BCryptHash(pass);

                var pwdResult = await _mw.ApiCallAsync(
                    "INSERT INTO passwords (password_hash) VALUES (@p0)",
                    new object[] { hash },
                    "insert"
                );
                long pwdId = pwdResult.GetProperty("lastId").GetInt64();

                await _mw.ApiCallAsync(
                    "INSERT INTO users (email, username, password_id) VALUES (@p0, @p1, @p2)",
                    new object[] { email, uname, pwdId },
                    "insert"
                );

                MessageBox.Show("Sikeres regisztráció! Most már bejelentkezhetsz.", "Siker",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                SwitchToLogin();
                _loginUser.Text = uname;
                _loginUser.Foreground = new SolidColorBrush(Color.FromRgb(0xF5, 0xF0, 0xE8));
            }
            catch (Exception ex)
            {
                ShowError("Hiba: " + ex.Message);
            }
        }
    }

    // ============================================
    // FŐOLDAL
    // ============================================
    public class MainPage : Page
    {
        private readonly MainWindow _mw;
        private WrapPanel _itemsPanel;
        private TextBox _searchBox;
        private List<ItemViewModel> _allItems = new List<ItemViewModel>();

        public MainPage(MainWindow mw)
        {
            _mw = mw;
            Build();
            Loaded += async (s, e) => await LoadItemsAsync();
        }

        private void Build()
        {
            Background = new SolidColorBrush(Color.FromRgb(0x05, 0x05, 0x05));
            var dock = new DockPanel();
            Content = dock;

            var topBar = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x0A, 0x0A, 0x0A)),
                Padding = new Thickness(10)
            };
            DockPanel.SetDock(topBar, Dock.Top);

            var topGrid = new Grid();
            topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var uploadBtn = MainWindow.MakeGhostButton("＋ Hirdetés feladása");
            uploadBtn.Margin = new Thickness(0, 0, 8, 0);
            uploadBtn.Click += (s, e) => _mw.NavigateToUpload();
            Grid.SetColumn(uploadBtn, 0);

            _searchBox = new TextBox
            {
                Background = new SolidColorBrush(Color.FromRgb(0x2D, 0x24, 0x18)),
                Foreground = new SolidColorBrush(Color.FromRgb(0x8A, 0x7A, 0x65)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x3D, 0x35, 0x28)),
                BorderThickness = new Thickness(1),
                FontSize = 14,
                Padding = new Thickness(14, 10, 14, 10),
                Margin = new Thickness(0, 0, 8, 0),
                Text = "Keresés...",
                VerticalAlignment = VerticalAlignment.Center
            };
            _searchBox.GotFocus += (s, e) =>
            {
                if (_searchBox.Text == "Keresés...")
                { _searchBox.Text = ""; _searchBox.Foreground = new SolidColorBrush(Color.FromRgb(0xF5, 0xF0, 0xE8)); }
            };
            _searchBox.LostFocus += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(_searchBox.Text))
                { _searchBox.Text = "Keresés..."; _searchBox.Foreground = new SolidColorBrush(Color.FromRgb(0x8A, 0x7A, 0x65)); }
            };
            _searchBox.TextChanged += (s, e) => FilterItems();
            Grid.SetColumn(_searchBox, 1);

            var accountBtn = MainWindow.MakeGhostButton("⚙ Fiók");
            accountBtn.Margin = new Thickness(0, 0, 8, 0);
            accountBtn.Click += (s, e) => _mw.NavigateToAccount();
            Grid.SetColumn(accountBtn, 2);

            var msgBtn = MainWindow.MakeGhostButton("💬 Üzenetek");
            msgBtn.Margin = new Thickness(0, 0, 8, 0);
            msgBtn.Click += (s, e) => _mw.NavigateToMessages();
            Grid.SetColumn(msgBtn, 3);

            if (MainWindow.IsAdmin)
            {
                var adminBtn = new Button
                {
                    Content = "🛡 Admin",
                    Background = new SolidColorBrush(Color.FromArgb(0x1F, 0xFF, 0xD7, 0x00)),
                    Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0xD7, 0x00)),
                    FontSize = 14,
                    FontWeight = FontWeights.Bold,
                    Padding = new Thickness(16, 10, 16, 10),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(0x4D, 0xFF, 0xD7, 0x00)),
                    BorderThickness = new Thickness(1),
                    Cursor = Cursors.Hand,
                    Margin = new Thickness(0, 0, 8, 0)
                };
                adminBtn.Click += (s, e) => _mw.NavigateToAdmin();
                Grid.SetColumn(adminBtn, 4);
                topGrid.Children.Add(adminBtn);
            }

            var logoutBtn = MainWindow.MakeRedGhostButton("🚪 Kilépés");
            logoutBtn.Click += (s, e) => _mw.NavigateToLogin();
            Grid.SetColumn(logoutBtn, 5);

            topGrid.Children.Add(uploadBtn);
            topGrid.Children.Add(_searchBox);
            topGrid.Children.Add(accountBtn);
            topGrid.Children.Add(msgBtn);
            topGrid.Children.Add(logoutBtn);
            topBar.Child = topGrid;
            dock.Children.Add(topBar);

            var sv = new ScrollViewer();
            _itemsPanel = new WrapPanel { Margin = new Thickness(20) };
            sv.Content = _itemsPanel;
            dock.Children.Add(sv);
        }

        private async Task LoadItemsAsync()
        {
            _allItems.Clear();
            try
            {
                var items = await _mw.ApiSelectAsync<Dictionary<string, JsonElement>>(
                    @"SELECT i.id, i.title, i.price, i.description, i.created_at, i.sold,
                             i.user_id, u.username AS seller_name,
                             (SELECT item_id FROM item_images WHERE item_id=i.id LIMIT 1) AS first_image
                      FROM items i
                      JOIN users u ON i.user_id = u.id
                      WHERE i.sold = FALSE
                      ORDER BY i.created_at DESC"
                );

                foreach (var row in items)
                {
                    _allItems.Add(new ItemViewModel
                    {
                        Id = row["id"].ToString(),
                        Title = row["title"].ToString(),
                        Price = decimal.Parse(row["price"].ToString(), System.Globalization.CultureInfo.InvariantCulture),
                        Description = row["description"].ValueKind == JsonValueKind.Null ? "" : row["description"].ToString(),
                        CreatedAt = DateTime.Parse(row["created_at"].ToString()),
                        IsSold = row["sold"].ToString() == "1",
                        SellerId = int.Parse(row["user_id"].ToString()),
                        SellerName = row["seller_name"].ValueKind == JsonValueKind.Null ? "" : row["seller_name"].ToString(),
                        FirstImagePath = row["first_image"].ValueKind == JsonValueKind.Null ? null : row["first_image"].ToString()
                    });
                }
            }
            catch (Exception ex) { MessageBox.Show("Adatbázis hiba: " + ex.Message); }

            DisplayItems(_allItems);
        }

        private void DisplayItems(IEnumerable<ItemViewModel> items)
        {
            _itemsPanel.Children.Clear();
            foreach (var item in items)
                _itemsPanel.Children.Add(BuildCard(item));
        }

        private Border BuildCard(ItemViewModel item)
        {
            var card = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x0F, 0x0F, 0x0F)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x29, 0xFF, 0x8C, 0x00)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Width = 180,
                Margin = new Thickness(6),
                Cursor = Cursors.Hand,
                Tag = item
            };
            card.MouseLeftButtonDown += async (s, e) =>
            {
                var w = new ProductDetailWindow(_mw, item);
                w.Owner = Window.GetWindow(this);
                w.ShowDialog();
                await LoadItemsAsync();
            };
            card.MouseEnter += (s, e) => card.BorderBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0x8C, 0x00));
            card.MouseLeave += (s, e) => card.BorderBrush = new SolidColorBrush(Color.FromArgb(0x29, 0xFF, 0x8C, 0x00));

            var stack = new StackPanel { Margin = new Thickness(8) };

            if (!string.IsNullOrEmpty(item.FirstImagePath))
            {
                try
                {
                    string imageUrl = "https://cuci.local.pepa.hu/" + item.FirstImagePath.Replace("\\", "/").TrimStart('/');
                    var img = new Image
                    {
                        Width = 164,
                        Height = 164,
                        Stretch = Stretch.UniformToFill,
                        Margin = new Thickness(-8, -8, -8, 4),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Source = new BitmapImage(new Uri(imageUrl, UriKind.Absolute))
                    };
                    stack.Children.Add(img);
                }
                catch { stack.Children.Add(MakeImagePlaceholder()); }
            }
            else stack.Children.Add(MakeImagePlaceholder());

            stack.Children.Add(new TextBlock
            {
                Text = item.Title,
                Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x9A, 0x1F)),
                FontWeight = FontWeights.Bold,
                FontSize = 13,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 2, 0, 2)
            });
            stack.Children.Add(new TextBlock
            {
                Text = item.PriceFormatted,
                Foreground = new SolidColorBrush(Color.FromRgb(0xF5, 0xF0, 0xE8)),
                FontSize = 16,
                FontWeight = FontWeights.Bold
            });
            stack.Children.Add(new TextBlock
            {
                Text = item.SellerName,
                Foreground = new SolidColorBrush(Color.FromRgb(0x8A, 0x7A, 0x65)),
                FontSize = 12
            });
            stack.Children.Add(new TextBlock
            {
                Text = item.CreatedAtFormatted,
                Foreground = new SolidColorBrush(Color.FromArgb(0x80, 0xFF, 0xFF, 0xFF)),
                FontSize = 11
            });

            card.Child = stack;
            return card;
        }

        private UIElement MakeImagePlaceholder()
        {
            return new Border
            {
                Width = 164,
                Height = 164,
                Background = new SolidColorBrush(Color.FromArgb(0x1A, 0xFF, 0x8C, 0x00)),
                CornerRadius = new CornerRadius(8),
                Margin = new Thickness(-8, -8, -8, 4),
                Child = new TextBlock
                {
                    Text = "📷",
                    FontSize = 32,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
        }

        private void FilterItems()
        {
            string q = _searchBox.Text.Trim().ToLower();
            if (string.IsNullOrEmpty(q) || q == "keresés...")
                DisplayItems(_allItems);
            else
                DisplayItems(_allItems.Where(i =>
                    i.Title.ToLower().Contains(q) ||
                    i.SellerName.ToLower().Contains(q) ||
                    i.Description.ToLower().Contains(q)));
        }
    }

    // ============================================================
    // TERMÉK RÉSZLETEK ABLAK (normál + admin mód)
    // ============================================================
    public class ProductDetailWindow : Window
    {
        private readonly MainWindow _mw;
        private readonly ItemViewModel _item;
        private readonly bool _isAdminMode;
        private Image _mainImg;
        private TextBlock _noImgText;
        private WrapPanel _thumbsPanel;
        private int _imgIdx;

        public ProductDetailWindow(MainWindow mw, ItemViewModel item, bool isAdminMode = false)
        {
            _mw = mw;
            _item = item;
            _isAdminMode = isAdminMode;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            Width = 900; Height = 650;
            ResizeMode = ResizeMode.CanResize;

            Loaded += async (s, e) => await LoadImagesAsync();
            Build();
        }

        private async Task LoadImagesAsync()
        {
            try
            {
                var images = await _mw.ApiSelectAsync<Dictionary<string, JsonElement>>(
                    "SELECT image_path FROM item_images WHERE item_id = @p0 ORDER BY sort_order",
                    new object[] { _item.Id });
                _item.AllImagePaths = images.Select(i => i["image_path"].ToString()).ToList();
                UpdateImg();
                BuildThumbs();
            }
            catch { }
        }

        private void Build()
        {
            var outer = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0xF5, 0x05, 0x05, 0x05)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x29, 0xFF, 0x8C, 0x00)),
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(16)
            };
            outer.MouseLeftButtonDown += (s, e) =>
            {
                if (e.GetPosition(outer).Y <= 40)
                    DragMove();
            };

            var outerGrid = new Grid();
            outer.Child = outerGrid;

            var titleBar = new Grid
            {
                Height = 44,
                VerticalAlignment = VerticalAlignment.Top
            };
            titleBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            titleBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var windowTitle = new TextBlock
            {
                Text = "Termék részletei" + (_isAdminMode ? " [ADMIN MÓD]" : ""),
                Foreground = new SolidColorBrush(Color.FromArgb(0x99, 0xFF, 0xFF, 0xFF)),
                FontSize = 13,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(16, 0, 0, 0)
            };
            Grid.SetColumn(windowTitle, 0);
            titleBar.Children.Add(windowTitle);

            var closeBtn = new Button
            {
                Content = "✕",
                Background = Brushes.Transparent,
                Foreground = new SolidColorBrush(Color.FromArgb(0x99, 0xFF, 0xFF, 0xFF)),
                FontSize = 16,
                Width = 36,
                Height = 36,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0)
            };
            closeBtn.Click += (s, e) => Close();
            Grid.SetColumn(closeBtn, 1);
            titleBar.Children.Add(closeBtn);

            Panel.SetZIndex(titleBar, 100);
            outerGrid.Children.Add(titleBar);

            var grid = new Grid { Margin = new Thickness(0, 44, 0, 0) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.5, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            outerGrid.Children.Add(grid);

            var galGrid = new Grid { Margin = new Thickness(16) };
            galGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            galGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var imgContainer = new Grid();
            var imgBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0x80, 0, 0, 0)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x29, 0xFF, 0x8C, 0x00)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12)
            };
            _mainImg = new Image { Stretch = Stretch.Uniform, Visibility = Visibility.Collapsed };
            _noImgText = new TextBlock
            {
                Text = "📷 Nincs kép",
                FontSize = 18,
                Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x9A, 0x1F)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            imgBorder.Child = new Grid { Children = { _mainImg, _noImgText } };
            imgContainer.Children.Add(imgBorder);

            if (_item.AllImagePaths.Count > 1)
            {
                var prevBtn = new Button
                {
                    Content = "❮",
                    Background = Brushes.Transparent,
                    Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x9A, 0x1F)),
                    FontSize = 24,
                    Width = 40,
                    Height = 40,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(8, 0, 0, 0),
                    BorderThickness = new Thickness(0),
                    Cursor = Cursors.Hand
                };
                prevBtn.Click += (s, e) => { _imgIdx = (_imgIdx - 1 + _item.AllImagePaths.Count) % _item.AllImagePaths.Count; UpdateImg(); };
                var nextBtn = new Button
                {
                    Content = "❯",
                    Background = Brushes.Transparent,
                    Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x9A, 0x1F)),
                    FontSize = 24,
                    Width = 40,
                    Height = 40,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 8, 0),
                    BorderThickness = new Thickness(0),
                    Cursor = Cursors.Hand
                };
                nextBtn.Click += (s, e) => { _imgIdx = (_imgIdx + 1) % _item.AllImagePaths.Count; UpdateImg(); };
                imgContainer.Children.Add(prevBtn);
                imgContainer.Children.Add(nextBtn);
            }

            Grid.SetRow(imgContainer, 0);
            galGrid.Children.Add(imgContainer);

            var thumbScroll = new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Height = 80,
                Margin = new Thickness(0, 8, 0, 0)
            };
            _thumbsPanel = new WrapPanel();
            thumbScroll.Content = _thumbsPanel;
            Grid.SetRow(thumbScroll, 1);
            galGrid.Children.Add(thumbScroll);

            Grid.SetColumn(galGrid, 0);
            grid.Children.Add(galGrid);

            var details = new ScrollViewer { Margin = new Thickness(0, 16, 16, 16) };
            var dStack = new StackPanel();
            details.Content = dStack;

            dStack.Children.Add(new TextBlock
            {
                Text = _item.Title,
                FontSize = 24,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x9A, 0x1F)),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 8)
            });
            dStack.Children.Add(new TextBlock
            {
                Text = _item.PriceFormatted,
                FontSize = 32,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x9A, 0x1F)),
                Margin = new Thickness(0, 0, 0, 8)
            });
            dStack.Children.Add(new TextBlock
            {
                Text = $"Eladó: {_item.SellerName}",
                FontSize = 14,
                Foreground = new SolidColorBrush(Color.FromRgb(0xF5, 0xF0, 0xE8)),
                Margin = new Thickness(0, 0, 0, 4)
            });
            dStack.Children.Add(new TextBlock
            {
                Text = _item.CreatedAtFormatted,
                FontSize = 13,
                Foreground = new SolidColorBrush(Color.FromArgb(0x66, 0xFF, 0xFF, 0xFF)),
                Margin = new Thickness(0, 0, 0, 16)
            });

            var descBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0x80, 0, 0, 0)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x29, 0xFF, 0x8C, 0x00)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(16),
                MaxHeight = 180,
                Margin = new Thickness(0, 0, 0, 16)
            };
            descBorder.Child = new ScrollViewer
            {
                Content = new TextBlock
                {
                    Text = string.IsNullOrEmpty(_item.Description) ? "Nincs leírás." : _item.Description,
                    TextWrapping = TextWrapping.Wrap,
                    FontSize = 14,
                    Foreground = new SolidColorBrush(Color.FromRgb(0xF5, 0xF0, 0xE8))
                }
            };
            dStack.Children.Add(descBorder);

            // ---- GOMBOK ----
            if (_isAdminMode)
            {
                var editBtn = MainWindow.MakeOrangeButton("✏️ Szerkesztés", 14);
                editBtn.Margin = new Thickness(0, 0, 0, 8);
                editBtn.Click += (s, e) =>
                {
                    string newTitle = MainWindow.ShowInputDialog("Cím:", "Szerkesztés", _item.Title);
                    if (!string.IsNullOrWhiteSpace(newTitle))
                        _ = EditItemAsync(newTitle);
                };
                dStack.Children.Add(editBtn);

                string soldText = _item.IsSold ? "🔄 Megjelölés aktívként" : "🔴 Megjelölés elkeltként";
                var soldBtnLocal = _item.IsSold
                    ? MainWindow.MakeGhostButton(soldText, 14)
                    : MainWindow.MakeRedGhostButton(soldText, 14);
                soldBtnLocal.Margin = new Thickness(0, 0, 0, 8);
                soldBtnLocal.Click += async (s2, e2) =>
                {
                    await _mw.ApiExecuteAsync("UPDATE items SET sold=@p0 WHERE id=@p1",
                        new object[] { _item.IsSold ? 0 : 1, _item.Id });
                    Close();
                };
                dStack.Children.Add(soldBtnLocal);

                var delBtn = MainWindow.MakeRedGhostButton("🗑️ Törlés", 14);
                delBtn.Click += async (s3, e3) =>
                {
                    if (MessageBox.Show("Biztosan törlöd ezt a terméket?", "Törlés",
                        MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                    {
                        await _mw.ApiExecuteAsync("DELETE FROM items WHERE id=@p0", new object[] { _item.Id });
                        Close();
                    }
                };
                dStack.Children.Add(delBtn);
            }
            else if (_item.IsSold)
            {
                dStack.Children.Add(new TextBlock
                {
                    Text = "🔴 Ez a termék már elkelt",
                    FontSize = 16,
                    Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x44, 0x44)),
                    Margin = new Thickness(0, 8, 0, 0)
                });
            }
            else
            {
                var buyBtn = new Button
                {
                    Content = "🛒 Vásárlás",
                    FontSize = 18,
                    FontWeight = FontWeights.Bold,
                    Foreground = Brushes.White,
                    Padding = new Thickness(0, 16, 0, 16),
                    BorderThickness = new Thickness(0),
                    Cursor = Cursors.Hand,
                    Margin = new Thickness(0, 16, 0, 0)
                };
                buyBtn.Background = new LinearGradientBrush(
                    Color.FromRgb(0x00, 0xC8, 0x51),
                    Color.FromRgb(0x00, 0x7E, 0x33), 45);
                var t = new ControlTemplate(typeof(Button));
                var bFef = new FrameworkElementFactory(typeof(Border));
                bFef.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
                bFef.SetValue(Border.CornerRadiusProperty, new CornerRadius(14));
                bFef.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Button.PaddingProperty));
                var cpFef = new FrameworkElementFactory(typeof(ContentPresenter));
                cpFef.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
                cpFef.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
                bFef.AppendChild(cpFef);
                t.VisualTree = bFef;
                buyBtn.Template = t;
                buyBtn.Click += (s4, e4) => { Close(); _mw.NavigateToPurchase(_item.Id); };
                dStack.Children.Add(buyBtn);
            }

            Grid.SetColumn(details, 1);
            grid.Children.Add(details);

            Content = outer;
        }

        private async Task EditItemAsync(string newTitle)
        {
            try
            {
                string newDesc = MainWindow.ShowInputDialog("Leírás:", "Szerkesztés", _item.Description ?? "");
                string newPriceStr = MainWindow.ShowInputDialog("Ár:", "Szerkesztés", _item.Price.ToString(System.Globalization.CultureInfo.InvariantCulture));

                if (decimal.TryParse(newPriceStr, out decimal newPrice) && newPrice >= 0)
                {
                    await _mw.ApiExecuteAsync(
                        "UPDATE items SET title=@p0, description=@p1, price=@p2 WHERE id=@p3",
                        new object[] { newTitle, newDesc ?? "", newPrice, _item.Id });
                    _item.Title = newTitle;
                    _item.Description = newDesc;
                    _item.Price = newPrice;
                    MessageBox.Show("Módosítások mentve!", "Siker", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex) { MessageBox.Show("Hiba: " + ex.Message); }
        }

        private void UpdateImg()
        {
            if (_item.AllImagePaths.Count > 0 && _imgIdx < _item.AllImagePaths.Count &&
                File.Exists(_item.AllImagePaths[_imgIdx]))
            {
                try
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.UriSource = new Uri(Path.GetFullPath(_item.AllImagePaths[_imgIdx]), UriKind.Absolute);
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.EndInit(); bmp.Freeze();
                    _mainImg.Source = bmp;
                    _mainImg.Visibility = Visibility.Visible;
                    _noImgText.Visibility = Visibility.Collapsed;
                    return;
                }
                catch { }
            }
            _mainImg.Visibility = Visibility.Collapsed;
            _noImgText.Visibility = Visibility.Visible;
        }

        private void BuildThumbs()
        {
            _thumbsPanel.Children.Clear();
            for (int i = 0; i < _item.AllImagePaths.Count; i++)
            {
                int idx = i;
                var tb = new Border
                {
                    Width = 64,
                    Height = 64,
                    Margin = new Thickness(3),
                    BorderBrush = i == _imgIdx
                        ? new SolidColorBrush(Color.FromRgb(0xFF, 0x8C, 0x00))
                        : new SolidColorBrush(Color.FromArgb(0x29, 0xFF, 0x8C, 0x00)),
                    BorderThickness = new Thickness(2),
                    CornerRadius = new CornerRadius(6),
                    Cursor = Cursors.Hand,
                    Background = new SolidColorBrush(Color.FromArgb(0x80, 0, 0, 0))
                };
                if (File.Exists(_item.AllImagePaths[i]))
                {
                    try
                    {
                        var bmp = new BitmapImage();
                        bmp.BeginInit();
                        bmp.UriSource = new Uri(Path.GetFullPath(_item.AllImagePaths[i]), UriKind.Absolute);
                        bmp.CacheOption = BitmapCacheOption.OnLoad;
                        bmp.DecodePixelWidth = 64;
                        bmp.EndInit(); bmp.Freeze();
                        tb.Child = new Image { Source = bmp, Stretch = Stretch.UniformToFill };
                    }
                    catch { }
                }
                tb.MouseLeftButtonDown += (s, e) => { _imgIdx = idx; UpdateImg(); };
                _thumbsPanel.Children.Add(tb);
            }
        }
    }

    // ============================================================
    // ADMIN OLDAL
    // ============================================================
    public class AdminPage : Page
    {
        private readonly MainWindow _mw;

        private TabControl _tabs;
        private TabItem _dashboardTab, _itemsTab, _usersTab, _ordersTab, _reportsTab, _conversationsTab, _vizsgalockTab;

        private TextBox _itemsSearchBox;
        private ListView _itemsListView;
        private int _itemsPage = 1, _itemsTotalPages = 1;
        private string _itemsSearch = "";

        private TextBox _usersSearchBox;
        private ListView _usersListView;
        private int _usersPage = 1, _usersTotalPages = 1;
        private string _usersSearch = "";

        private TextBox _ordersSearchBox;
        private ListView _ordersListView;
        private int _ordersPage = 1, _ordersTotalPages = 1;
        private string _ordersSearch = "";

        private ListView _reportsListView;

        private ListBox _convPartnersList;
        private StackPanel _convMessagesPanel;
        private ScrollViewer _convMsgScroll;
        private int? _convSelectedUser1, _convSelectedUser2;
        private string _convUser1Name, _convUser2Name;

        private Button _vlToggleBtn;
        private ListView _vlExceptionsList;
        private ComboBox _vlUserCombo;
        private bool _vlLocked;

        private TextBlock _dashUsers, _dashItems, _dashOrders, _dashReports;

        private const int PER_PAGE = 25;

        public AdminPage(MainWindow mw)
        {
            _mw = mw;
            Build();
            Loaded += async (s, e) =>
            {
                await LoadDashboardAsync();
                await LoadVizsgalockStatusAsync();
            };
        }

        private void Build()
        {
            Background = new SolidColorBrush(Color.FromRgb(0x05, 0x05, 0x05));
            var dock = new DockPanel();

            var topBar = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x0A, 0x0A, 0x0A)),
                Padding = new Thickness(10)
            };
            DockPanel.SetDock(topBar, Dock.Top);

            var topRow = new StackPanel { Orientation = Orientation.Horizontal };
            var backBtn = MainWindow.MakeGhostButton("← Vissza");
            backBtn.Click += (s, e) => _mw.NavigateToMain();
            topRow.Children.Add(backBtn);

            topRow.Children.Add(new TextBlock
            {
                Text = "ADMIN TERMINAL",
                Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x9A, 0x1F)),
                FontWeight = FontWeights.Bold,
                FontSize = 18,
                Margin = new Thickness(20, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            });

            _vlToggleBtn = MainWindow.MakeRedGhostButton("⚠ VIZSGALOCK");
            _vlToggleBtn.Margin = new Thickness(20, 0, 0, 0);
            _vlToggleBtn.Click += async (s2, e2) => await ToggleVizsgalockAsync();
            topRow.Children.Add(_vlToggleBtn);

            var purgeBtn = MainWindow.MakeRedGhostButton("⚠ VIZSGAPURGE");
            purgeBtn.Margin = new Thickness(10, 0, 0, 0);
            purgeBtn.Click += async (s3, e3) => await PurgeAsync();
            topRow.Children.Add(purgeBtn);

            topBar.Child = topRow;
            dock.Children.Add(topBar);

            _tabs = new TabControl
            {
                Margin = new Thickness(10),
                Background = new SolidColorBrush(Color.FromRgb(0x05, 0x05, 0x05)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x29, 0xFF, 0x8C, 0x00))
            };

            _tabs.Items.Add(_dashboardTab = CreateTab("■ FŐOLDAL", BuildDashboardTab()));
            _tabs.Items.Add(_itemsTab = CreateTab("◧ TERMÉKEK", BuildItemsTab()));
            _tabs.Items.Add(_usersTab = CreateTab("◈ FELHASZNÁLÓK", BuildUsersTab()));
            _tabs.Items.Add(_ordersTab = CreateTab("📦 RENDELÉSEK", BuildOrdersTab()));
            _tabs.Items.Add(_reportsTab = CreateTab("⚠ REPORTOK", BuildReportsTab()));
            _tabs.Items.Add(_conversationsTab = CreateTab("💬 BESZÉLGETÉSEK", BuildConversationsTab()));
            _tabs.Items.Add(_vizsgalockTab = CreateTab("🔒 VIZSGALOCK", BuildVizsgalockTab()));

            _tabs.SelectionChanged += async (s4, e4) =>
            {
                if (_tabs.SelectedItem == _itemsTab && _itemsListView.Items.Count == 0) await LoadItemsAsync();
                if (_tabs.SelectedItem == _usersTab && _usersListView.Items.Count == 0) await LoadUsersAsync();
                if (_tabs.SelectedItem == _ordersTab && _ordersListView.Items.Count == 0) await LoadOrdersAsync();
                if (_tabs.SelectedItem == _reportsTab && _reportsListView.Items.Count == 0) await LoadReportsAsync();
                if (_tabs.SelectedItem == _conversationsTab) await LoadConversationsAsync();
                if (_tabs.SelectedItem == _vizsgalockTab) await LoadVizsgalockStatusAsync();
            };

            dock.Children.Add(_tabs);
            Content = dock;
        }

        private TabItem CreateTab(string header, UIElement content)
        {
            return new TabItem
            {
                Header = header,
                Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x9A, 0x1F)),
                Background = new SolidColorBrush(Color.FromRgb(0x0F, 0x0F, 0x0F)),
                FontWeight = FontWeights.Bold,
                Content = content
            };
        }

        private Brush CardBg = new SolidColorBrush(Color.FromRgb(0x0F, 0x0F, 0x0F));
        private Brush Border1 = new SolidColorBrush(Color.FromArgb(0x29, 0xFF, 0x8C, 0x00));
        private Brush Accent = new SolidColorBrush(Color.FromRgb(0xFF, 0x9A, 0x1F));
        private Brush TextFg = new SolidColorBrush(Color.FromRgb(0xF5, 0xF0, 0xE8));
        private Brush Muted = new SolidColorBrush(Color.FromRgb(0x8A, 0x7A, 0x65));

        // ==================== DASHBOARD ====================
        private UIElement BuildDashboardTab()
        {
            var sv = new ScrollViewer();
            var grid = new Grid { Margin = new Thickness(20) };
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            _dashUsers = AddDashCard(grid, "◈ FELHASZNÁLÓK", "Regisztrált fiókok", 0);
            _dashItems = AddDashCard(grid, "◧ TERMÉKEK", "Aktív hirdetések", 1);
            _dashOrders = AddDashCard(grid, "📦 RENDELÉSEK", "Megrendelések", 2);
            _dashReports = AddDashCard(grid, "⚠ REPORTOK", "Bejelentett elemek", 3);

            var info = new Border
            {
                Background = CardBg,
                BorderBrush = Border1,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(16),
                Margin = new Thickness(0, 16, 0, 0)
            };
            Grid.SetRow(info, 1); Grid.SetColumnSpan(info, 4);
            var infoStack = new StackPanel();
            infoStack.Children.Add(new TextBlock { Text = "SYSTEM STATUS: ONLINE", Foreground = Accent, FontSize = 13, FontFamily = new FontFamily("Consolas") });
            infoStack.Children.Add(new TextBlock { Text = $"DATABASE: CUCIDB  //  ADMIN: {MainWindow.LoggedInUsername}", Foreground = Muted, FontSize = 12, FontFamily = new FontFamily("Consolas") });
            info.Child = infoStack;
            grid.Children.Add(info);
            sv.Content = grid;
            return sv;
        }

        private TextBlock AddDashCard(Grid grid, string label, string sub, int col)
        {
            var card = new Border
            {
                Background = CardBg,
                BorderBrush = Border1,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(20),
                Margin = new Thickness(8)
            };
            var stack = new StackPanel();
            stack.Children.Add(new TextBlock { Text = "■ " + label, Foreground = Muted, FontSize = 11, Margin = new Thickness(0, 0, 0, 8) });
            var num = new TextBlock { Text = "—", Foreground = Accent, FontSize = 42, FontWeight = FontWeights.Bold };
            stack.Children.Add(num);
            stack.Children.Add(new TextBlock { Text = sub, Foreground = Muted, FontSize = 11, Margin = new Thickness(0, 6, 0, 0) });
            card.Child = stack;
            Grid.SetColumn(card, col);
            grid.Children.Add(card);
            return num;
        }

        private async Task LoadDashboardAsync()
        {
            try
            {
                var users = await _mw.ApiScalarAsync<int>("SELECT COUNT(*) FROM users");
                var items = await _mw.ApiScalarAsync<int>("SELECT COUNT(*) FROM items");
                var orders = await _mw.ApiScalarAsync<int>("SELECT COUNT(*) FROM orders");
                var reports = await _mw.ApiScalarAsync<int>("SELECT COUNT(*) FROM reports");
                var msgRep = await _mw.ApiScalarAsync<int>("SELECT COUNT(*) FROM message_reports");
                _dashUsers.Text = users.ToString("N0");
                _dashItems.Text = items.ToString("N0");
                _dashOrders.Text = orders.ToString("N0");
                _dashReports.Text = (reports + msgRep).ToString("N0");
            }
            catch { }
        }

        // ==================== TERMÉKEK ====================
        private UIElement BuildItemsTab()
        {
            var dock = new DockPanel();
            var topPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(10) };
            _itemsSearchBox = new TextBox
            {
                Background = new SolidColorBrush(Color.FromRgb(0x2D, 0x24, 0x18)),
                Foreground = TextFg,
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x3D, 0x35, 0x28)),
                BorderThickness = new Thickness(1),
                FontSize = 13,
                Padding = new Thickness(10, 8, 10, 8),
                Width = 300
            };
            _itemsSearchBox.KeyDown += (s, e) => { if (e.Key == Key.Return) { _itemsPage = 1; _ = LoadItemsAsync(); } };
            var searchBtn = MainWindow.MakeGhostButton("KERESÉS", 12);
            searchBtn.Click += (s, e) => { _itemsPage = 1; _ = LoadItemsAsync(); };
            topPanel.Children.Add(_itemsSearchBox);
            topPanel.Children.Add(searchBtn);
            DockPanel.SetDock(topPanel, Dock.Top);
            dock.Children.Add(topPanel);

            _itemsListView = new ListView
            {
                Background = new SolidColorBrush(Color.FromRgb(0x05, 0x05, 0x05)),
                Foreground = TextFg,
                BorderThickness = new Thickness(0)
            };
            var gridView = new GridView();
            gridView.Columns.Add(new GridViewColumn { Header = "ID", Width = 100, DisplayMemberBinding = new Binding("Id") });
            gridView.Columns.Add(new GridViewColumn { Header = "Cím", Width = 200, DisplayMemberBinding = new Binding("Title") });
            gridView.Columns.Add(new GridViewColumn { Header = "Eladó", Width = 120, DisplayMemberBinding = new Binding("SellerName") });
            gridView.Columns.Add(new GridViewColumn { Header = "Ár", Width = 100, DisplayMemberBinding = new Binding("PriceFormatted") });
            gridView.Columns.Add(new GridViewColumn { Header = "Státusz", Width = 80, DisplayMemberBinding = new Binding("StatusText") });
            _itemsListView.View = gridView;
            _itemsListView.MouseDoubleClick += ItemsList_DoubleClick;
            dock.Children.Add(_itemsListView);

            var pagePanel = BuildPaginationPanel(() => { _ = LoadItemsAsync(); }, () => _itemsPage, v => _itemsPage = v, () => _itemsTotalPages);
            DockPanel.SetDock(pagePanel, Dock.Bottom);
            dock.Children.Add(pagePanel);
            return dock;
        }

        private async Task LoadItemsAsync()
        {
            _itemsSearch = _itemsSearchBox?.Text.Trim() ?? "";
            try
            {
                string where = "";
                var pars = new List<object>();
                if (!string.IsNullOrEmpty(_itemsSearch))
                {
                    where = " AND (i.title LIKE @p0 OR i.description LIKE @p1)";
                    pars.Add("%" + _itemsSearch + "%");
                    pars.Add("%" + _itemsSearch + "%");
                }
                int total = await _mw.ApiScalarAsync<int>(
                    $"SELECT COUNT(*) FROM items i WHERE 1=1{where}", pars.ToArray());
                _itemsTotalPages = Math.Max(1, (int)Math.Ceiling(total / (double)PER_PAGE));
                int offset = (_itemsPage - 1) * PER_PAGE;

                var itemsPars = new List<object>(pars) { offset, PER_PAGE };
                var items = await _mw.ApiSelectAsync<Dictionary<string, JsonElement>>(
                    $"SELECT i.id, i.title, i.price, i.sold, u.username AS seller_name FROM items i JOIN users u ON i.user_id=u.id WHERE 1=1{where} ORDER BY i.created_at DESC LIMIT @p{pars.Count},@p{pars.Count + 1}",
                    itemsPars.ToArray());

                _itemsListView.Items.Clear();
                foreach (var r in items)
                {
                    _itemsListView.Items.Add(new ItemViewModel
                    {
                        Id = r["id"].ToString(),
                        Title = r["title"].ToString(),
                        Price = decimal.Parse(r["price"].ToString(), System.Globalization.CultureInfo.InvariantCulture),
                        IsSold = r["sold"].ToString() == "1",
                        SellerName = r["seller_name"].ToString()
                    });
                }
            }
            catch (Exception ex) { MessageBox.Show("Hiba: " + ex.Message); }
        }

        private async void ItemsList_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (_itemsListView.SelectedItem is ItemViewModel item)
            {
                try
                {
                    var details = await _mw.ApiSelectAsync<Dictionary<string, JsonElement>>(
                        "SELECT i.*, u.username AS seller_name FROM items i JOIN users u ON i.user_id=u.id WHERE i.id=@p0",
                        new object[] { item.Id });
                    if (details.Count > 0)
                    {
                        var d = details[0];
                        var fullItem = new ItemViewModel
                        {
                            Id = d["id"].ToString(),
                            Title = d["title"].ToString(),
                            Price = decimal.Parse(d["price"].ToString(), System.Globalization.CultureInfo.InvariantCulture),
                            Description = d["description"].ValueKind == JsonValueKind.Null ? "" : d["description"].ToString(),
                            CreatedAt = DateTime.Parse(d["created_at"].ToString()),
                            IsSold = d["sold"].ToString() == "1",
                            SellerId = int.Parse(d["user_id"].ToString()),
                            SellerName = d["seller_name"].ToString()
                        };
                        var w = new ProductDetailWindow(_mw, fullItem, isAdminMode: true);
                        w.Owner = Window.GetWindow(this);
                        w.ShowDialog();
                        await LoadItemsAsync();
                        await LoadDashboardAsync();
                    }
                }
                catch { }
            }
        }

        // ==================== FELHASZNÁLÓK ====================
        private UIElement BuildUsersTab()
        {
            var dock = new DockPanel();
            var topPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(10) };
            _usersSearchBox = new TextBox
            {
                Background = new SolidColorBrush(Color.FromRgb(0x2D, 0x24, 0x18)),
                Foreground = TextFg,
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x3D, 0x35, 0x28)),
                BorderThickness = new Thickness(1),
                FontSize = 13,
                Padding = new Thickness(10, 8, 10, 8),
                Width = 300
            };
            _usersSearchBox.KeyDown += (s, e) => { if (e.Key == Key.Return) { _usersPage = 1; _ = LoadUsersAsync(); } };
            var searchBtn = MainWindow.MakeGhostButton("KERESÉS", 12);
            searchBtn.Click += (s, e) => { _usersPage = 1; _ = LoadUsersAsync(); };
            topPanel.Children.Add(_usersSearchBox);
            topPanel.Children.Add(searchBtn);
            DockPanel.SetDock(topPanel, Dock.Top);
            dock.Children.Add(topPanel);

            _usersListView = new ListView
            {
                Background = new SolidColorBrush(Color.FromRgb(0x05, 0x05, 0x05)),
                Foreground = TextFg,
                BorderThickness = new Thickness(0)
            };
            var gv = new GridView();
            gv.Columns.Add(new GridViewColumn { Header = "ID", Width = 60, DisplayMemberBinding = new Binding("Id") });
            gv.Columns.Add(new GridViewColumn { Header = "Felhasználónév", Width = 150, DisplayMemberBinding = new Binding("Username") });
            gv.Columns.Add(new GridViewColumn { Header = "Email", Width = 200, DisplayMemberBinding = new Binding("Email") });
            gv.Columns.Add(new GridViewColumn { Header = "Szerepkör", Width = 80, DisplayMemberBinding = new Binding("RoleText") });
            gv.Columns.Add(new GridViewColumn { Header = "Hirdetések", Width = 80, DisplayMemberBinding = new Binding("ItemCount") });
            gv.Columns.Add(new GridViewColumn { Header = "Regisztrált", Width = 100, DisplayMemberBinding = new Binding("CreatedAtFormatted") });
            _usersListView.View = gv;
            _usersListView.MouseDoubleClick += UsersList_DoubleClick;
            dock.Children.Add(_usersListView);

            var pagePanel = BuildPaginationPanel(() => { _ = LoadUsersAsync(); }, () => _usersPage, v => _usersPage = v, () => _usersTotalPages);
            DockPanel.SetDock(pagePanel, Dock.Bottom);
            dock.Children.Add(pagePanel);
            return dock;
        }

        private async Task LoadUsersAsync()
        {
            _usersSearch = _usersSearchBox?.Text.Trim() ?? "";
            try
            {
                string where = "";
                var pars = new List<object>();
                if (!string.IsNullOrEmpty(_usersSearch))
                {
                    where = " AND (u.username LIKE @p0 OR u.email LIKE @p1)";
                    pars.Add("%" + _usersSearch + "%");
                    pars.Add("%" + _usersSearch + "%");
                }
                int total = await _mw.ApiScalarAsync<int>($"SELECT COUNT(*) FROM users u WHERE 1=1{where}", pars.ToArray());
                _usersTotalPages = Math.Max(1, (int)Math.Ceiling(total / (double)PER_PAGE));
                int offset = (_usersPage - 1) * PER_PAGE;

                var itemsPars = new List<object>(pars) { offset, PER_PAGE };
                var users = await _mw.ApiSelectAsync<Dictionary<string, JsonElement>>(
                    $"SELECT u.id, u.username, u.email, (SELECT COUNT(*) FROM admins WHERE user_id=u.id) AS is_admin, (SELECT COUNT(*) FROM items WHERE user_id=u.id) AS item_count, u.created_at FROM users u WHERE 1=1{where} ORDER BY u.created_at DESC LIMIT @p{pars.Count},@p{pars.Count + 1}",
                    itemsPars.ToArray());

                _usersListView.Items.Clear();
                foreach (var r in users)
                {
                    _usersListView.Items.Add(new UserViewModel
                    {
                        Id = int.Parse(r["id"].ToString()),
                        Username = r["username"].ToString(),
                        Email = r["email"].ToString(),
                        IsAdmin = int.Parse(r["is_admin"].ToString()) > 0,
                        ItemCount = int.Parse(r["item_count"].ToString()),
                        CreatedAt = DateTime.Parse(r["created_at"].ToString())
                    });
                }
            }
            catch (Exception ex) { MessageBox.Show("Hiba: " + ex.Message); }
        }

        private async void UsersList_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (_usersListView.SelectedItem is UserViewModel user)
            {
                string newName = MainWindow.ShowInputDialog("Felhasználónév:", "Szerkesztés – " + user.Username, user.Username);
                if (!string.IsNullOrWhiteSpace(newName))
                {
                    await _mw.ApiExecuteAsync("UPDATE users SET username=@p0 WHERE id=@p1",
                        new object[] { newName, user.Id });
                    await LoadUsersAsync();
                }
            }
        }

        // ==================== RENDELÉSEK ====================
        private UIElement BuildOrdersTab()
        {
            var dock = new DockPanel();
            var topPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(10) };
            _ordersSearchBox = new TextBox
            {
                Background = new SolidColorBrush(Color.FromRgb(0x2D, 0x24, 0x18)),
                Foreground = TextFg,
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x3D, 0x35, 0x28)),
                BorderThickness = new Thickness(1),
                FontSize = 13,
                Padding = new Thickness(10, 8, 10, 8),
                Width = 300
            };
            _ordersSearchBox.KeyDown += (s, e) => { if (e.Key == Key.Return) { _ordersPage = 1; _ = LoadOrdersAsync(); } };
            var searchBtn = MainWindow.MakeGhostButton("KERESÉS", 12);
            searchBtn.Click += (s, e) => { _ordersPage = 1; _ = LoadOrdersAsync(); };
            topPanel.Children.Add(_ordersSearchBox);
            topPanel.Children.Add(searchBtn);
            DockPanel.SetDock(topPanel, Dock.Top);
            dock.Children.Add(topPanel);

            _ordersListView = new ListView
            {
                Background = new SolidColorBrush(Color.FromRgb(0x05, 0x05, 0x05)),
                Foreground = TextFg,
                BorderThickness = new Thickness(0)
            };
            var gv = new GridView();
            gv.Columns.Add(new GridViewColumn { Header = "ID", Width = 100, DisplayMemberBinding = new Binding("Id") });
            gv.Columns.Add(new GridViewColumn { Header = "Termék", Width = 180, DisplayMemberBinding = new Binding("ItemTitle") });
            gv.Columns.Add(new GridViewColumn { Header = "Vevő", Width = 120, DisplayMemberBinding = new Binding("BuyerName") });
            gv.Columns.Add(new GridViewColumn { Header = "Eladó", Width = 120, DisplayMemberBinding = new Binding("SellerName") });
            gv.Columns.Add(new GridViewColumn { Header = "Összeg", Width = 100, DisplayMemberBinding = new Binding("PriceFormatted") });
            gv.Columns.Add(new GridViewColumn { Header = "Státusz", Width = 80, DisplayMemberBinding = new Binding("Status") });
            gv.Columns.Add(new GridViewColumn { Header = "Fizetés", Width = 80, DisplayMemberBinding = new Binding("PaymentMethod") });
            gv.Columns.Add(new GridViewColumn { Header = "Dátum", Width = 100, DisplayMemberBinding = new Binding("CreatedAtFormatted") });
            _ordersListView.View = gv;
            _ordersListView.MouseDoubleClick += async (s5, e5) =>
            {
                if (_ordersListView.SelectedItem is OrderViewModel o)
                {
                    if (MessageBox.Show($"Törlöd a(z) {o.Id} rendelést?\nA termék újra elérhető lesz.", "Törlés", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                    {
                        await _mw.ApiExecuteAsync("UPDATE items SET sold=0 WHERE id=@p0", new object[] { o.ItemId });
                        await _mw.ApiExecuteAsync("DELETE FROM orders WHERE id=@p0", new object[] { o.Id });
                        await LoadOrdersAsync();
                        await LoadDashboardAsync();
                    }
                }
            };
            dock.Children.Add(_ordersListView);

            var pagePanel = BuildPaginationPanel(() => { _ = LoadOrdersAsync(); }, () => _ordersPage, v => _ordersPage = v, () => _ordersTotalPages);
            DockPanel.SetDock(pagePanel, Dock.Bottom);
            dock.Children.Add(pagePanel);
            return dock;
        }

        private async Task LoadOrdersAsync()
        {
            _ordersSearch = _ordersSearchBox?.Text.Trim() ?? "";
            try
            {
                string where = "";
                var pars = new List<object>();
                if (!string.IsNullOrEmpty(_ordersSearch))
                {
                    where = " AND (i.title LIKE @p0 OR o.id LIKE @p1 OR buyer.username LIKE @p2 OR seller.username LIKE @p3)";
                    pars.Add("%" + _ordersSearch + "%"); pars.Add("%" + _ordersSearch + "%");
                    pars.Add("%" + _ordersSearch + "%"); pars.Add("%" + _ordersSearch + "%");
                }
                int total = await _mw.ApiScalarAsync<int>(
                    $"SELECT COUNT(*) FROM orders o JOIN items i ON o.item_id=i.id JOIN users buyer ON o.buyer_id=buyer.id JOIN users seller ON o.seller_id=seller.id WHERE 1=1{where}", pars.ToArray());
                _ordersTotalPages = Math.Max(1, (int)Math.Ceiling(total / (double)PER_PAGE));
                int offset = (_ordersPage - 1) * PER_PAGE;

                var itemsPars = new List<object>(pars) { offset, PER_PAGE };
                var orders = await _mw.ApiSelectAsync<Dictionary<string, JsonElement>>(
                    $"SELECT o.*, i.title AS item_title, i.price AS item_price, buyer.username AS buyer_name, seller.username AS seller_name FROM orders o JOIN items i ON o.item_id=i.id JOIN users buyer ON o.buyer_id=buyer.id JOIN users seller ON o.seller_id=seller.id WHERE 1=1{where} ORDER BY o.created_at DESC LIMIT @p{pars.Count},@p{pars.Count + 1}",
                    itemsPars.ToArray());

                _ordersListView.Items.Clear();
                foreach (var r in orders)
                {
                    _ordersListView.Items.Add(new OrderViewModel
                    {
                        Id = r["id"].ToString(),
                        ItemTitle = r["item_title"].ToString(),
                        ItemId = r["item_id"].ToString(),
                        BuyerName = r["buyer_name"].ToString(),
                        BuyerId = int.Parse(r["buyer_id"].ToString()),
                        SellerName = r["seller_name"].ToString(),
                        SellerId = int.Parse(r["seller_id"].ToString()),
                        ItemPrice = decimal.Parse(r["item_price"].ToString(), System.Globalization.CultureInfo.InvariantCulture),
                        Status = r["status"].ToString(),
                        PaymentMethod = r["payment_method"].ToString(),
                        CreatedAt = DateTime.Parse(r["created_at"].ToString())
                    });
                }
            }
            catch (Exception ex) { MessageBox.Show("Hiba: " + ex.Message); }
        }

        // ==================== REPORTOK ====================
        private UIElement BuildReportsTab()
        {
            var dock = new DockPanel();
            _reportsListView = new ListView
            {
                Background = new SolidColorBrush(Color.FromRgb(0x05, 0x05, 0x05)),
                Foreground = TextFg,
                BorderThickness = new Thickness(0)
            };
            var gv = new GridView();
            gv.Columns.Add(new GridViewColumn { Header = "ID", Width = 60, DisplayMemberBinding = new Binding("Id") });
            gv.Columns.Add(new GridViewColumn { Header = "Típus", Width = 80, DisplayMemberBinding = new Binding("TypeIcon") });
            gv.Columns.Add(new GridViewColumn { Header = "Tárgy", Width = 200, DisplayMemberBinding = new Binding("RefTitle") });
            gv.Columns.Add(new GridViewColumn { Header = "Bejelentő", Width = 120, DisplayMemberBinding = new Binding("ReporterName") });
            gv.Columns.Add(new GridViewColumn { Header = "Érintett", Width = 120, DisplayMemberBinding = new Binding("TargetName") });
            gv.Columns.Add(new GridViewColumn { Header = "Indok", Width = 250, DisplayMemberBinding = new Binding("Reason") });
            gv.Columns.Add(new GridViewColumn { Header = "Dátum", Width = 100, DisplayMemberBinding = new Binding("CreatedAtFormatted") });
            _reportsListView.View = gv;
            _reportsListView.MouseDoubleClick += async (s6, e6) =>
            {
                if (_reportsListView.SelectedItem is ReportViewModel r)
                {
                    if (MessageBox.Show($"Törlöd a(z) {r.Id} reportot?", "Törlés", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                    {
                        string tbl = r.ReportType == "item" ? "reports" : "message_reports";
                        await _mw.ApiExecuteAsync($"DELETE FROM {tbl} WHERE id=@p0", new object[] { r.Id });
                        await LoadReportsAsync();
                        await LoadDashboardAsync();
                    }
                }
            };
            dock.Children.Add(_reportsListView);
            return dock;
        }

        private async Task LoadReportsAsync()
        {
            try
            {
                var reports = await _mw.ApiSelectAsync<Dictionary<string, JsonElement>>(
                    @"SELECT r.id, 'item' AS report_type, r.item_id AS ref_id, i.title AS ref_title,
                             u.username AS reporter_name, u.id AS reporter_id,
                             owner.username AS target_name, owner.id AS target_id,
                             r.reason, r.status, r.created_at
                      FROM reports r
                      JOIN items i ON r.item_id=i.id
                      JOIN users u ON r.user_id=u.id
                      JOIN users owner ON i.user_id=owner.id
                      UNION ALL
                      SELECT mr.id, 'message' AS report_type, mr.message_id, 'Üzenet' AS ref_title,
                             rep.username AS reporter_name, rep.id AS reporter_id,
                             snd.username AS target_name, snd.id AS target_id,
                             mr.reason, mr.status, mr.created_at
                      FROM message_reports mr
                      JOIN uzenetek m ON mr.message_id=m.id
                      JOIN users rep ON mr.reporter_user_id=rep.id
                      JOIN users snd ON m.sender_id=snd.id
                      ORDER BY created_at DESC");

                _reportsListView.Items.Clear();
                foreach (var r in reports)
                {
                    _reportsListView.Items.Add(new ReportViewModel
                    {
                        Id = int.Parse(r["id"].ToString()),
                        ReportType = r["report_type"].ToString(),
                        RefId = r["ref_id"].ToString(),
                        RefTitle = r["ref_title"].ToString(),
                        ReporterName = r["reporter_name"].ToString(),
                        ReporterId = int.Parse(r["reporter_id"].ToString()),
                        TargetName = r["target_name"].ToString(),
                        TargetId = int.Parse(r["target_id"].ToString()),
                        Reason = r["reason"].ToString(),
                        Status = r["status"].ToString(),
                        CreatedAt = DateTime.Parse(r["created_at"].ToString())
                    });
                }
            }
            catch { _reportsListView.Items.Clear(); }
        }

        // ==================== BESZÉLGETÉSEK ====================
        private UIElement BuildConversationsTab()
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(280) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var leftDock = new DockPanel();
            var leftTop = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x0A, 0x0A, 0x0A)),
                Padding = new Thickness(10)
            };
            DockPanel.SetDock(leftTop, Dock.Top);
            leftTop.Child = new TextBlock { Text = "Beszélgetések", Foreground = Accent, FontWeight = FontWeights.Bold, FontSize = 14 };
            leftDock.Children.Add(leftTop);

            _convPartnersList = new ListBox
            {
                Background = new SolidColorBrush(Color.FromRgb(0x05, 0x05, 0x05)),
                Foreground = TextFg,
                BorderThickness = new Thickness(0)
            };
            leftDock.Children.Add(_convPartnersList);
            Grid.SetColumn(leftDock, 0);

            var sep = new Border { Background = Border1, Width = 1 };
            Grid.SetColumn(sep, 1);

            var rightDock = new DockPanel();
            var chatHeader = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x0A, 0x0A, 0x0A)),
                Padding = new Thickness(16, 12, 16, 12)
            };
            DockPanel.SetDock(chatHeader, Dock.Top);
            chatHeader.Child = new TextBlock { Text = "Üzenetek", Foreground = Accent, FontWeight = FontWeights.Bold, FontSize = 14 };
            rightDock.Children.Add(chatHeader);

            _convMsgScroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new Thickness(16) };
            _convMessagesPanel = new StackPanel();
            _convMsgScroll.Content = _convMessagesPanel;
            rightDock.Children.Add(_convMsgScroll);

            Grid.SetColumn(rightDock, 2);

            grid.Children.Add(leftDock);
            grid.Children.Add(sep);
            grid.Children.Add(rightDock);
            return grid;
        }

        private async Task LoadConversationsAsync()
        {
            try
            {
                var partners = await _mw.ApiSelectAsync<Dictionary<string, JsonElement>>(
                    @"SELECT LEAST(u1.id, u2.id) AS user1, GREATEST(u1.id, u2.id) AS user2,
                             u1.username AS user1_name, u2.username AS user2_name,
                             MAX(m.sent_at) AS last_msg
                      FROM uzenetek m
                      JOIN users u1 ON (u1.id=m.sender_id OR u1.id=m.receiver_id)
                      JOIN users u2 ON (u2.id=m.sender_id OR u2.id=m.receiver_id)
                      WHERE u1.id < u2.id
                      GROUP BY user1, user2, user1_name, user2_name
                      ORDER BY last_msg DESC");

                _convPartnersList.Items.Clear();
                foreach (var r in partners)
                {
                    int u1 = int.Parse(r["user1"].ToString());
                    int u2 = int.Parse(r["user2"].ToString());
                    string n1 = r["user1_name"].ToString();
                    string n2 = r["user2_name"].ToString();
                    int partnerId = u1 == MainWindow.LoggedInUserId ? u2 : u1;
                    string partnerName = u1 == MainWindow.LoggedInUserId ? n2 : n1;

                    var pvm = new PartnerViewModel
                    {
                        Id = partnerId,
                        Username = partnerName,
                        LastMessageAt = DateTime.Parse(r["last_msg"].ToString())
                    };

                    var itemBorder = new Border
                    {
                        Padding = new Thickness(12, 8, 12, 8),
                        Cursor = Cursors.Hand,
                        Tag = pvm
                    };

                    var row = new StackPanel { Orientation = Orientation.Horizontal };
                    var avatar = new Border
                    {
                        Width = 36,
                        Height = 36,
                        CornerRadius = new CornerRadius(18),
                        Background = new SolidColorBrush(Color.FromArgb(0x29, 0xFF, 0x8C, 0x00)),
                        Margin = new Thickness(0, 0, 10, 0)
                    };
                    avatar.Child = new TextBlock
                    {
                        Text = pvm.AvatarInitial,
                        Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x9A, 0x1F)),
                        FontWeight = FontWeights.Bold,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    row.Children.Add(avatar);

                    var nameStack = new StackPanel();
                    nameStack.Children.Add(new TextBlock
                    {
                        Text = partnerName,
                        Foreground = new SolidColorBrush(Color.FromRgb(0xF5, 0xF0, 0xE8)),
                        FontWeight = FontWeights.Normal,
                        FontSize = 14
                    });
                    nameStack.Children.Add(new TextBlock
                    {
                        Text = pvm.LastMessageTimeFormatted,
                        Foreground = new SolidColorBrush(Color.FromRgb(0x8A, 0x7A, 0x65)),
                        FontSize = 11
                    });
                    row.Children.Add(nameStack);

                    itemBorder.Child = row;

                    var listBoxItem = new ListBoxItem
                    {
                        Content = itemBorder,
                        Background = Brushes.Transparent,
                        Tag = pvm
                    };
                    listBoxItem.Selected += (s, e) =>
                    {
                        if (listBoxItem.Tag is PartnerViewModel p)
                        {
                            _convSelectedUser1 = MainWindow.LoggedInUserId;
                            _convSelectedUser2 = p.Id;
                            _convUser1Name = MainWindow.LoggedInUsername;
                            _convUser2Name = p.Username;
                            _ = LoadConversationMessagesAsync();
                        }
                    };

                    _convPartnersList.Items.Add(listBoxItem);
                }
            }
            catch { }
        }

        private async Task LoadConversationMessagesAsync()
        {
            _convMessagesPanel.Children.Clear();
            if (!_convSelectedUser1.HasValue || !_convSelectedUser2.HasValue) return;
            try
            {
                var msgs = await _mw.ApiSelectAsync<Dictionary<string, JsonElement>>(
                    "SELECT id, sender_id, message, sent_at FROM uzenetek WHERE (sender_id=@p0 AND receiver_id=@p1) OR (sender_id=@p2 AND receiver_id=@p3) ORDER BY sent_at ASC",
                    new object[] { _convSelectedUser1, _convSelectedUser2, _convSelectedUser2, _convSelectedUser1 });

                foreach (var m in msgs)
                {
                    bool own = int.Parse(m["sender_id"].ToString()) == MainWindow.LoggedInUserId;
                    var bubble = new Border
                    {
                        CornerRadius = new CornerRadius(12),
                        Padding = new Thickness(12, 8, 12, 8),
                        MaxWidth = 400,
                        Margin = new Thickness(0, 3, 0, 3),
                        HorizontalAlignment = own ? HorizontalAlignment.Right : HorizontalAlignment.Left
                    };
                    bubble.Background = own
                        ? (Brush)new LinearGradientBrush(Color.FromRgb(0xFF, 0x8C, 0x00), Color.FromRgb(0xC8, 0x50, 0x00), 45)
                        : (Brush)new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E));
                    var stack = new StackPanel();
                    stack.Children.Add(new TextBlock
                    {
                        Text = m["message"].ToString(),
                        TextWrapping = TextWrapping.Wrap,
                        Foreground = Brushes.White,
                        FontSize = 13
                    });
                    stack.Children.Add(new TextBlock
                    {
                        Text = DateTime.Parse(m["sent_at"].ToString()).ToString("HH:mm"),
                        Foreground = new SolidColorBrush(Color.FromArgb(0xAA, 0xFF, 0xFF, 0xFF)),
                        FontSize = 10,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Margin = new Thickness(0, 4, 0, 0)
                    });
                    bubble.Child = stack;
                    _convMessagesPanel.Children.Add(bubble);
                }
                _convMsgScroll.ScrollToBottom();
            }
            catch { }
        }

        // ==================== VIZSGALOCK ====================
        private UIElement BuildVizsgalockTab()
        {
            var sv = new ScrollViewer();
            var stack = new StackPanel { Margin = new Thickness(20), MaxWidth = 500 };

            stack.Children.Add(new TextBlock
            {
                Text = "⚠️ VIZSGALOCK",
                FontSize = 22,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x44, 0x44)),
                Margin = new Thickness(0, 0, 0, 16)
            });

            var vlToggleBtnLocal = MainWindow.MakeRedGhostButton("VIZSGALOCK ÁTKAPCSOLÁSA");
            vlToggleBtnLocal.Height = 50; vlToggleBtnLocal.FontSize = 18;
            vlToggleBtnLocal.Click += async (s8, e8) => await ToggleVizsgalockAsync();
            _vlToggleBtn = vlToggleBtnLocal;
            stack.Children.Add(vlToggleBtnLocal);

            stack.Children.Add(new TextBlock
            {
                Text = "Kivételek",
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = TextFg,
                Margin = new Thickness(0, 20, 0, 8)
            });

            _vlExceptionsList = new ListView
            {
                Background = new SolidColorBrush(Color.FromRgb(0x0F, 0x0F, 0x0F)),
                Foreground = TextFg,
                MaxHeight = 200,
                BorderThickness = new Thickness(0)
            };
            var gv = new GridView();
            gv.Columns.Add(new GridViewColumn { Header = "ID", Width = 60, DisplayMemberBinding = new Binding("Id") });
            gv.Columns.Add(new GridViewColumn { Header = "Felhasználónév", Width = 200, DisplayMemberBinding = new Binding("Username") });
            _vlExceptionsList.View = gv;
            _vlExceptionsList.MouseDoubleClick += async (s9, e9) =>
            {
                if (_vlExceptionsList.SelectedItem is UserViewModel u)
                {
                    await _mw.ApiExecuteAsync("DELETE FROM vizsgalock_exceptions WHERE user_id=@p0", new object[] { u.Id });
                    await LoadVizsgalockStatusAsync();
                }
            };
            stack.Children.Add(_vlExceptionsList);

            var addRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };
            _vlUserCombo = new ComboBox
            {
                Background = new SolidColorBrush(Color.FromRgb(0x2D, 0x24, 0x18)),
                Foreground = TextFg,
                Width = 250,
                Height = 35
            };
            addRow.Children.Add(_vlUserCombo);
            var addBtn = MainWindow.MakeRedGhostButton("+ HOZZÁAD");
            addBtn.Click += async (s10, e10) =>
            {
                if (_vlUserCombo.SelectedItem != null)
                {
                    var uid = (int)_vlUserCombo.SelectedValue;
                    await _mw.ApiExecuteAsync("INSERT IGNORE INTO vizsgalock_exceptions (user_id) VALUES (@p0)", new object[] { uid });
                    await LoadVizsgalockStatusAsync();
                }
            };
            addRow.Children.Add(addBtn);
            stack.Children.Add(addRow);

            sv.Content = stack;
            return sv;
        }

        private async Task LoadVizsgalockStatusAsync()
        {
            try
            {
                _vlLocked = await _mw.CheckVizsgalockAsync();
                if (_vlToggleBtn != null)
                {
                    _vlToggleBtn.Content = _vlLocked ? "⚠️ VIZSGALOCK: ON ⚠️" : "VIZSGALOCK: OFF";
                    _vlToggleBtn.Foreground = _vlLocked
                        ? new SolidColorBrush(Color.FromRgb(0xFF, 0x00, 0x00))
                        : new SolidColorBrush(Color.FromRgb(0xFF, 0x44, 0x44));
                }

                var exceptions = await _mw.ApiSelectAsync<Dictionary<string, JsonElement>>(
                    "SELECT u.id, u.username FROM vizsgalock_exceptions ve JOIN users u ON ve.user_id=u.id ORDER BY u.username");
                _vlExceptionsList?.Items.Clear();
                foreach (var e in exceptions)
                    _vlExceptionsList?.Items.Add(new UserViewModel { Id = int.Parse(e["id"].ToString()), Username = e["username"].ToString() });

                var available = await _mw.ApiSelectAsync<Dictionary<string, JsonElement>>(
                    "SELECT u.id, u.username FROM users u WHERE u.id NOT IN (SELECT user_id FROM admins) AND u.id NOT IN (SELECT user_id FROM vizsgalock_exceptions) ORDER BY u.username");
                _vlUserCombo?.Items.Clear();
                foreach (var a in available)
                    _vlUserCombo?.Items.Add(new { Id = int.Parse(a["id"].ToString()), Name = a["username"].ToString() });
                if (_vlUserCombo != null)
                {
                    _vlUserCombo.DisplayMemberPath = "Name";
                    _vlUserCombo.SelectedValuePath = "Id";
                }
            }
            catch { }
        }

        private async Task ToggleVizsgalockAsync()
        {
            if (MessageBox.Show("Biztosan átkapcsolod a VIZSGALOCK állapotát?", "Megerősítés",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                await _mw.ApiExecuteAsync(
                    "UPDATE vizsgalock_settings SET is_locked=@p0 WHERE id=1",
                    new object[] { !_vlLocked });
                await LoadVizsgalockStatusAsync();
            }
        }

        private async Task PurgeAsync()
        {
            if (MessageBox.Show(
                "Ez véglegesen TÖRLI az összes nem-admin felhasználót!\n(kivéve: gabi, martin, cuci, admin)\n\nBiztosan folytatod?",
                "⚠️ VIZSGAPURGE", MessageBoxButton.YesNo, MessageBoxImage.Error) == MessageBoxResult.Yes)
            {
                try
                {
                    int affected = await _mw.ApiExecuteAsync(
                        "DELETE FROM users WHERE LOWER(username) NOT IN ('gabi','martin','cuci','admin') AND id NOT IN (SELECT user_id FROM admins)",
                        null, "delete");
                    MessageBox.Show($"Purge kész! Törölt felhasználók: {affected}", "Kész",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    await LoadDashboardAsync();
                }
                catch (Exception ex) { MessageBox.Show("Hiba: " + ex.Message); }
            }
        }

        // ==================== SEGÉDESZKÖZÖK ====================
        private UIElement BuildPaginationPanel(Action loadAction, Func<int> getPage, Action<int> setPage, Func<int> getTotal)
        {
            var panel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 10, 0, 10)
            };
            var prevBtn = MainWindow.MakeGhostButton("◄ ELŐZŐ", 12);
            prevBtn.Click += (s, e) => { if (getPage() > 1) { setPage(getPage() - 1); loadAction(); } };
            panel.Children.Add(prevBtn);

            panel.Children.Add(new TextBlock
            {
                Text = $" {getPage()} / {getTotal()} ",
                Foreground = Accent,
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(16, 0, 16, 0)
            });

            var nextBtn = MainWindow.MakeGhostButton("KÖVETKEZŐ ►", 12);
            nextBtn.Click += (s, e) => { if (getPage() < getTotal()) { setPage(getPage() + 1); loadAction(); } };
            panel.Children.Add(nextBtn);

            return panel;
        }
    }

    // ============================================
    // FIÓK OLDAL
    // ============================================
    public class AccountPage : Page
    {
        private readonly MainWindow _mw;
        private TextBox _unameBox, _emailBox;
        private PasswordBox _pwdBox;

        public AccountPage(MainWindow mw)
        {
            _mw = mw;
            Build();
            Loaded += async (s, e) => await LoadDataAsync();
        }

        private void Build()
        {
            Background = new SolidColorBrush(Color.FromRgb(0x05, 0x05, 0x05));
            var sv = new ScrollViewer();
            var stack = new StackPanel { Margin = new Thickness(40) };

            var backBtn = MainWindow.MakeGhostButton("← Vissza");
            backBtn.HorizontalAlignment = HorizontalAlignment.Left;
            backBtn.Click += (s, e) => _mw.NavigateToMain();
            stack.Children.Add(backBtn);

            stack.Children.Add(new TextBlock
            {
                Text = "Fiók beállítások",
                FontSize = 22,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x9A, 0x1F)),
                Margin = new Thickness(0, 16, 0, 16)
            });

            stack.Children.Add(MainWindow.MakeLabel("Felhasználónév"));
            _unameBox = MainWindow.MakeInput();
            _unameBox.Margin = new Thickness(0, 0, 0, 12);
            stack.Children.Add(_unameBox);

            stack.Children.Add(MainWindow.MakeLabel("Email"));
            _emailBox = MainWindow.MakeInput();
            _emailBox.Margin = new Thickness(0, 0, 0, 12);
            stack.Children.Add(_emailBox);

            stack.Children.Add(MainWindow.MakeLabel("Új jelszó (ha módosítani szeretnéd)"));
            _pwdBox = new PasswordBox
            {
                Background = new SolidColorBrush(Color.FromRgb(0x2D, 0x24, 0x18)),
                Foreground = new SolidColorBrush(Color.FromRgb(0xF5, 0xF0, 0xE8)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x3D, 0x35, 0x28)),
                BorderThickness = new Thickness(1),
                FontSize = 14,
                Padding = new Thickness(14, 12, 14, 12),
                Margin = new Thickness(0, 0, 0, 16)
            };
            stack.Children.Add(_pwdBox);

            var saveBtn = MainWindow.MakeOrangeButton("Mentés", 15);
            saveBtn.Width = 200;
            saveBtn.HorizontalAlignment = HorizontalAlignment.Left;
            saveBtn.Click += async (s2, e2) => await SaveAsync();
            stack.Children.Add(saveBtn);

            stack.Children.Add(new TextBlock
            {
                Text = "Saját hirdetéseim",
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x9A, 0x1F)),
                Margin = new Thickness(0, 32, 0, 16)
            });

            var myItemsPanel = new StackPanel();
            stack.Children.Add(myItemsPanel);
            Loaded += async (s3, e3) => await LoadMyItemsAsync(myItemsPanel);

            sv.Content = stack;
            Content = sv;
        }

        private async Task LoadMyItemsAsync(StackPanel panel)
        {
            try
            {
                var items = await _mw.ApiSelectAsync<Dictionary<string, JsonElement>>(
                    "SELECT id, title, price, sold, created_at FROM items WHERE user_id = @p0 ORDER BY created_at DESC",
                    new object[] { MainWindow.LoggedInUserId });

                foreach (var r in items)
                {
                    string iid = r["id"].ToString();
                    string ititle = r["title"].ToString();
                    decimal iprice = decimal.Parse(r["price"].ToString(), System.Globalization.CultureInfo.InvariantCulture);
                    bool isold = r["sold"].ToString() == "1";

                    var row = new Border
                    {
                        Background = new SolidColorBrush(Color.FromRgb(0x0F, 0x0F, 0x0F)),
                        BorderBrush = new SolidColorBrush(Color.FromArgb(0x29, 0xFF, 0x8C, 0x00)),
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(10),
                        Padding = new Thickness(16, 12, 16, 12),
                        Margin = new Thickness(0, 0, 0, 8)
                    };
                    var rowGrid = new Grid();
                    rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                    var info = new StackPanel();
                    info.Children.Add(new TextBlock
                    {
                        Text = ititle,
                        Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x9A, 0x1F)),
                        FontWeight = FontWeights.Bold,
                        FontSize = 14
                    });
                    info.Children.Add(new TextBlock
                    {
                        Text = $"{iprice:N0} Ft  ·  {(isold ? "🔴 Elkelt" : "🟢 Aktív")}",
                        Foreground = new SolidColorBrush(Color.FromRgb(0x8A, 0x7A, 0x65)),
                        FontSize = 12
                    });
                    Grid.SetColumn(info, 0);
                    rowGrid.Children.Add(info);

                    if (!isold)
                    {
                        var soldBtn = MainWindow.MakeGhostButton("Megjelölés elkeltként", 12);
                        soldBtn.Margin = new Thickness(8, 0, 8, 0);
                        string capturedId = iid;
                        soldBtn.Click += async (s4, e4) => { await MarkAsSoldAsync(capturedId); panel.Children.Clear(); await LoadMyItemsAsync(panel); };
                        Grid.SetColumn(soldBtn, 1);
                        rowGrid.Children.Add(soldBtn);
                    }

                    var delBtn = MainWindow.MakeRedGhostButton("Törlés", 12);
                    string capturedId2 = iid;
                    delBtn.Click += async (s5, e5) => { await DeleteItemAsync(capturedId2); panel.Children.Clear(); await LoadMyItemsAsync(panel); };
                    Grid.SetColumn(delBtn, 2);
                    rowGrid.Children.Add(delBtn);

                    row.Child = rowGrid;
                    panel.Children.Add(row);
                }
            }
            catch (Exception ex) { MessageBox.Show("Hiba: " + ex.Message); }
        }

        private async Task MarkAsSoldAsync(string itemId)
        {
            if (MessageBox.Show("Biztosan elkeltnek jelölöd ezt a terméket?", "Megerősítés",
                MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                try
                {
                    await _mw.ApiExecuteAsync(
                        "UPDATE items SET sold=1 WHERE id=@p0 AND user_id=@p1",
                        new object[] { itemId, MainWindow.LoggedInUserId });
                }
                catch (Exception ex) { MessageBox.Show("Hiba: " + ex.Message); }
            }
        }

        private async Task DeleteItemAsync(string itemId)
        {
            if (MessageBox.Show("Biztosan törlöd ezt a hirdetést?", "Törlés",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                try
                {
                    await _mw.ApiExecuteAsync(
                        "DELETE FROM items WHERE id=@p0 AND user_id=@p1",
                        new object[] { itemId, MainWindow.LoggedInUserId });
                }
                catch (Exception ex) { MessageBox.Show("Hiba: " + ex.Message); }
            }
        }

        private async Task LoadDataAsync()
        {
            try
            {
                var users = await _mw.ApiSelectAsync<Dictionary<string, JsonElement>>(
                    "SELECT username, email FROM users WHERE id = @p0",
                    new object[] { MainWindow.LoggedInUserId });
                if (users.Count > 0)
                {
                    _unameBox.Text = users[0]["username"].ToString();
                    _emailBox.Text = users[0]["email"].ToString();
                }
            }
            catch (Exception ex) { MessageBox.Show("Adatbázis hiba: " + ex.Message); }
        }

        private async Task SaveAsync()
        {
            string uname = _unameBox.Text.Trim();
            string email = _emailBox.Text.Trim();
            string pwd = _pwdBox.Password;

            if (string.IsNullOrWhiteSpace(uname)) { MessageBox.Show("Felhasználónév nem lehet üres!"); return; }
            if (!email.Contains("@")) { MessageBox.Show("Érvénytelen email!"); return; }

            try
            {
                var check = await _mw.ApiSelectAsync<Dictionary<string, JsonElement>>(
                    "SELECT id FROM users WHERE (username=@p0 OR email=@p1) AND id!=@p2",
                    new object[] { uname, email, MainWindow.LoggedInUserId });
                if (check.Count > 0)
                { MessageBox.Show("A felhasználónév vagy email már foglalt!"); return; }

                await _mw.ApiExecuteAsync(
                    "UPDATE users SET username=@p0, email=@p1 WHERE id=@p2",
                    new object[] { uname, email, MainWindow.LoggedInUserId });

                if (!string.IsNullOrWhiteSpace(pwd) && pwd.Length >= 6)
                {
                    string hash = _mw.BCryptHash(pwd);
                    var pwdResult = await _mw.ApiCallAsync(
                        "INSERT INTO passwords (password_hash) VALUES (@p0)",
                        new object[] { hash },
                        "insert");
                    long pwdId = pwdResult.GetProperty("lastId").GetInt64();
                    await _mw.ApiExecuteAsync(
                        "UPDATE users SET password_id=@p0 WHERE id=@p1",
                        new object[] { pwdId, MainWindow.LoggedInUserId });
                }

                MainWindow.LoggedInUsername = uname;
                MessageBox.Show("Adatok mentve!", "Siker", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex) { MessageBox.Show("Hiba: " + ex.Message); }
        }
    }

    // ============================================
    // FELTÖLTÉS OLDAL
    // ============================================
    public class UploadPage : Page
    {
        private readonly MainWindow _mw;
        private List<byte[]> _imgs = new List<byte[]>();
        private TextBox _titleBox, _descBox, _priceBox;
        private TextBlock _imgCountText;

        public UploadPage(MainWindow mw)
        {
            _mw = mw;
            Build();
        }

        private void Build()
        {
            Background = new SolidColorBrush(Color.FromRgb(0x05, 0x05, 0x05));
            var sv = new ScrollViewer();
            var stack = new StackPanel { Margin = new Thickness(40) };

            var backBtn = MainWindow.MakeGhostButton("← Vissza");
            backBtn.HorizontalAlignment = HorizontalAlignment.Left;
            backBtn.Click += (s, e) => _mw.NavigateToMain();
            stack.Children.Add(backBtn);

            stack.Children.Add(new TextBlock
            {
                Text = "Új hirdetés",
                FontSize = 22,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x9A, 0x1F)),
                Margin = new Thickness(0, 16, 0, 8)
            });

            var imgRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 16) };
            var imgBtn = MainWindow.MakeGhostButton("📸 Képek kiválasztása");
            imgBtn.Click += PickImages;
            imgRow.Children.Add(imgBtn);
            _imgCountText = new TextBlock
            {
                Foreground = new SolidColorBrush(Color.FromRgb(0x8A, 0x7A, 0x65)),
                FontSize = 13,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(12, 0, 0, 0),
                Text = "Nincs kép kiválasztva"
            };
            imgRow.Children.Add(_imgCountText);
            stack.Children.Add(imgRow);

            stack.Children.Add(MainWindow.MakeLabel("Cím *"));
            _titleBox = MainWindow.MakeInput();
            _titleBox.Margin = new Thickness(0, 0, 0, 12);
            stack.Children.Add(_titleBox);

            stack.Children.Add(MainWindow.MakeLabel("Leírás *"));
            _descBox = new TextBox
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
            stack.Children.Add(_descBox);

            stack.Children.Add(MainWindow.MakeLabel("Ár (Ft) *"));
            _priceBox = MainWindow.MakeInput();
            _priceBox.Margin = new Thickness(0, 0, 0, 20);
            stack.Children.Add(_priceBox);

            var submitBtn = MainWindow.MakeOrangeButton("Hirdetés feladása", 15);
            submitBtn.Click += async (s2, e2) => await SubmitAsync();
            stack.Children.Add(submitBtn);

            sv.Content = stack;
            Content = sv;
        }

        private void PickImages(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "Képfájlok|*.jpg;*.jpeg;*.png;*.gif;*.webp",
                Multiselect = true
            };
            if (dlg.ShowDialog() == true)
            {
                foreach (var f in dlg.FileNames)
                    _imgs.Add(File.ReadAllBytes(f));
                _imgCountText.Text = $"{_imgs.Count} kép kiválasztva";
                _imgCountText.Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xC8, 0x51));
            }
        }

        private async Task SubmitAsync()
        {
            string title = _titleBox.Text.Trim();
            string desc = _descBox.Text.Trim();
            string priceStr = _priceBox.Text.Trim();

            if (!_mw.CanPerformWriteOperation())
            { MessageBox.Show("VIZSGALOCK aktív!", "Hiba", MessageBoxButton.OK, MessageBoxImage.Error); return; }
            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(desc) ||
                !decimal.TryParse(priceStr, out decimal price) || price < 0)
            { MessageBox.Show("Minden mező kitöltése kötelező!"); return; }
            if (_imgs.Count == 0)
            { MessageBox.Show("Legalább egy kép szükséges!"); return; }

            try
            {
                string itemId = _mw.GenerateId();
                await _mw.ApiCallAsync(
                    "INSERT INTO items (id,user_id,title,description,price) VALUES(@p0,@p1,@p2,@p3,@p4)",
                    new object[] { itemId, MainWindow.LoggedInUserId, title, desc, price },
                    "insert");

                string uploadDir = Path.Combine("uploads", itemId);
                Directory.CreateDirectory(uploadDir);

                for (int i = 0; i < _imgs.Count; i++)
                {
                    byte[] imgData = _mw.ResizeImage(_imgs[i]);
                    string fname = $"{Guid.NewGuid()}_{i}.jpg";
                    string fpath = Path.Combine(uploadDir, fname);
                    File.WriteAllBytes(fpath, imgData);

                    await _mw.ApiCallAsync(
                        "INSERT INTO item_images(item_id,image_path,image_filename,is_primary,sort_order) VALUES(@p0,@p1,@p2,@p3,@p4)",
                        new object[] { itemId, fpath, fname, i == 0, i },
                        "insert");
                }

                MessageBox.Show("Hirdetés sikeresen feladva!", "Siker", MessageBoxButton.OK, MessageBoxImage.Information);
                _mw.NavigateToMain();
            }
            catch (Exception ex) { MessageBox.Show("Hiba: " + ex.Message); }
        }
    }

    // ============================================
    // ÜZENETEK OLDAL
    // ============================================
    public class MessagesPage : Page
    {
        private readonly MainWindow _mw;
        private ListBox _partnersList;
        private StackPanel _msgContainer;
        private TextBox _msgInput;
        private ScrollViewer _msgScroll;
        private int? _partnerId;
        private List<PartnerViewModel> _partners = new List<PartnerViewModel>();

        public MessagesPage(MainWindow mw)
        {
            _mw = mw;
            Build();
            Loaded += async (s, e) => await LoadPartnersAsync();
        }

        private void Build()
        {
            Background = new SolidColorBrush(Color.FromRgb(0x05, 0x05, 0x05));
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(260) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var leftDock = new DockPanel();
            var leftTop = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x0A, 0x0A, 0x0A)),
                Padding = new Thickness(10)
            };
            DockPanel.SetDock(leftTop, Dock.Top);
            var leftTopStack = new StackPanel();
            leftTopStack.Children.Add(new TextBlock
            {
                Text = "Beszélgetések",
                Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x9A, 0x1F)),
                FontWeight = FontWeights.Bold,
                FontSize = 15,
                Margin = new Thickness(0, 0, 0, 8)
            });
            var backBtn = MainWindow.MakeGhostButton("← Vissza", 12);
            backBtn.HorizontalAlignment = HorizontalAlignment.Left;
            backBtn.Click += (s, e) => _mw.NavigateToMain();
            leftTopStack.Children.Add(backBtn);
            leftTop.Child = leftTopStack;

            _partnersList = new ListBox
            {
                Background = new SolidColorBrush(Color.FromRgb(0x05, 0x05, 0x05)),
                Foreground = new SolidColorBrush(Color.FromRgb(0xF5, 0xF0, 0xE8)),
                BorderThickness = new Thickness(0)
            };
            _partnersList.SelectionChanged += async (s2, e2) =>
            {
                if (_partnersList.SelectedItem is PartnerViewModel p)
                {
                    _partnerId = p.Id;
                    await LoadMessagesAsync(p.Id);
                }
            };

            leftDock.Children.Add(leftTop);
            leftDock.Children.Add(_partnersList);
            Grid.SetColumn(leftDock, 0);

            var sep = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0x29, 0xFF, 0x8C, 0x00))
            };
            Grid.SetColumn(sep, 1);

            var rightDock = new DockPanel();

            var rightTop = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x0A, 0x0A, 0x0A)),
                Padding = new Thickness(16, 12, 16, 12)
            };
            DockPanel.SetDock(rightTop, Dock.Top);
            rightTop.Child = new TextBlock
            {
                Text = "Üzenetek",
                Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x9A, 0x1F)),
                FontWeight = FontWeights.Bold,
                FontSize = 15
            };

            var inputBar = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x0A, 0x0A, 0x0A)),
                Padding = new Thickness(10)
            };
            DockPanel.SetDock(inputBar, Dock.Bottom);

            var inputGrid = new Grid();
            inputGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            inputGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _msgInput = new TextBox
            {
                Background = new SolidColorBrush(Color.FromRgb(0x2D, 0x24, 0x18)),
                Foreground = new SolidColorBrush(Color.FromRgb(0xF5, 0xF0, 0xE8)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x3D, 0x35, 0x28)),
                BorderThickness = new Thickness(1),
                FontSize = 14,
                Padding = new Thickness(14, 10, 14, 10),
                VerticalAlignment = VerticalAlignment.Center
            };
            _msgInput.KeyDown += async (s3, e3) => { if (e3.Key == Key.Return && !Keyboard.IsKeyDown(Key.LeftShift)) { await SendAsync(); e3.Handled = true; } };
            Grid.SetColumn(_msgInput, 0);

            var sendBtn = MainWindow.MakeOrangeButton("➤");
            sendBtn.Width = 44; sendBtn.Height = 44;
            sendBtn.Margin = new Thickness(8, 0, 0, 0);
            sendBtn.Click += async (s4, e4) => await SendAsync();
            Grid.SetColumn(sendBtn, 1);

            inputGrid.Children.Add(_msgInput);
            inputGrid.Children.Add(sendBtn);
            inputBar.Child = inputGrid;

            _msgScroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Padding = new Thickness(16, 8, 16, 8)
            };
            _msgContainer = new StackPanel();
            _msgScroll.Content = _msgContainer;

            rightDock.Children.Add(rightTop);
            rightDock.Children.Add(inputBar);
            rightDock.Children.Add(_msgScroll);
            Grid.SetColumn(rightDock, 2);

            grid.Children.Add(leftDock);
            grid.Children.Add(sep);
            grid.Children.Add(rightDock);
            Content = grid;
        }

        private async Task LoadPartnersAsync()
        {
            _partners.Clear();
            try
            {
                var partners = await _mw.ApiSelectAsync<Dictionary<string, JsonElement>>(
                    @"SELECT u.id, u.username,
                             MAX(m.sent_at) AS last_msg,
                             SUM(CASE WHEN m.receiver_id=@p0 AND m.is_read=0 THEN 1 ELSE 0 END) AS unread
                      FROM users u
                      JOIN uzenetek m ON (
                          (m.sender_id=u.id AND m.receiver_id=@p1)
                          OR (m.receiver_id=u.id AND m.sender_id=@p2)
                      )
                      WHERE u.id != @p3
                      GROUP BY u.id, u.username
                      ORDER BY last_msg DESC",
                    new object[] { MainWindow.LoggedInUserId, MainWindow.LoggedInUserId, MainWindow.LoggedInUserId, MainWindow.LoggedInUserId });

                foreach (var r in partners)
                {
                    _partners.Add(new PartnerViewModel
                    {
                        Id = int.Parse(r["id"].ToString()),
                        Username = r["username"].ToString(),
                        UnreadCount = r["unread"].ValueKind == JsonValueKind.Null ? 0 : int.Parse(r["unread"].ToString()),
                        LastMessageAt = r["last_msg"].ValueKind == JsonValueKind.Null ? DateTime.MinValue : DateTime.Parse(r["last_msg"].ToString()),
                    });
                }
            }
            catch { }

            _partnersList.Items.Clear();
            foreach (var p in _partners)
            {
                var item = new Border
                {
                    Padding = new Thickness(12, 8, 12, 8),
                    Cursor = Cursors.Hand,
                    Tag = p
                };
                var row = new StackPanel { Orientation = Orientation.Horizontal };
                var avatar = new Border
                {
                    Width = 36,
                    Height = 36,
                    CornerRadius = new CornerRadius(18),
                    Background = new SolidColorBrush(Color.FromArgb(0x29, 0xFF, 0x8C, 0x00)),
                    Margin = new Thickness(0, 0, 10, 0)
                };
                avatar.Child = new TextBlock
                {
                    Text = p.AvatarInitial,
                    Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x9A, 0x1F)),
                    FontWeight = FontWeights.Bold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                row.Children.Add(avatar);

                var nameStack = new StackPanel();
                nameStack.Children.Add(new TextBlock
                {
                    Text = p.Username + (p.HasUnread ? $" ({p.UnreadCount})" : ""),
                    Foreground = new SolidColorBrush(p.HasUnread ? Color.FromRgb(0xFF, 0xFF, 0xFF) : Color.FromRgb(0xF5, 0xF0, 0xE8)),
                    FontWeight = p.HasUnread ? FontWeights.Bold : FontWeights.Normal,
                    FontSize = 14
                });
                nameStack.Children.Add(new TextBlock
                {
                    Text = p.LastMessageTimeFormatted,
                    Foreground = new SolidColorBrush(Color.FromRgb(0x8A, 0x7A, 0x65)),
                    FontSize = 11
                });
                row.Children.Add(nameStack);
                item.Child = row;

                var li = new ListBoxItem { Content = item, Background = Brushes.Transparent, Tag = p };
                li.Selected += async (s5, e5) => { if (li.Tag is PartnerViewModel pv) { _partnerId = pv.Id; await LoadMessagesAsync(pv.Id); } };
                _partnersList.Items.Add(li);
            }
        }

        private async Task LoadMessagesAsync(int partnerId)
        {
            _msgContainer.Children.Clear();
            try
            {
                await _mw.ApiExecuteAsync(
                    "UPDATE uzenetek SET is_read=1 WHERE sender_id=@p0 AND receiver_id=@p1 AND is_read=0",
                    new object[] { partnerId, MainWindow.LoggedInUserId });

                var messages = await _mw.ApiSelectAsync<Dictionary<string, JsonElement>>(
                    @"SELECT id, sender_id, message, sent_at
                      FROM uzenetek
                      WHERE (sender_id=@p0 AND receiver_id=@p1)
                         OR (sender_id=@p2 AND receiver_id=@p3)
                      ORDER BY sent_at ASC",
                    new object[] { MainWindow.LoggedInUserId, partnerId, partnerId, MainWindow.LoggedInUserId });

                foreach (var r in messages)
                {
                    bool isOwn = int.Parse(r["sender_id"].ToString()) == MainWindow.LoggedInUserId;
                    string text = r["message"].ToString();
                    string time = DateTime.Parse(r["sent_at"].ToString()).ToString("HH:mm");
                    _msgContainer.Children.Add(BuildBubble(text, time, isOwn));
                }
            }
            catch { }

            _msgScroll.ScrollToBottom();
        }

        private UIElement BuildBubble(string text, string time, bool isOwn)
        {
            var outer = new Grid { Margin = new Thickness(0, 3, 0, 3) };
            var bubble = new Border
            {
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(14, 10, 14, 10),
                MaxWidth = 400,
                HorizontalAlignment = isOwn ? HorizontalAlignment.Right : HorizontalAlignment.Left
            };

            if (isOwn)
                bubble.Background = new LinearGradientBrush(
                    Color.FromRgb(0xFF, 0x8C, 0x00), Color.FromRgb(0xC8, 0x50, 0x00), 45);
            else
                bubble.Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E));

            var stack = new StackPanel();
            stack.Children.Add(new TextBlock
            {
                Text = text,
                TextWrapping = TextWrapping.Wrap,
                Foreground = Brushes.White,
                FontSize = 14
            });
            stack.Children.Add(new TextBlock
            {
                Text = time,
                Foreground = new SolidColorBrush(Color.FromArgb(0xAA, 0xFF, 0xFF, 0xFF)),
                FontSize = 11,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 3, 0, 0)
            });
            bubble.Child = stack;
            outer.Children.Add(bubble);
            return outer;
        }

        private async Task SendAsync()
        {
            if (!_partnerId.HasValue || string.IsNullOrWhiteSpace(_msgInput.Text)) return;
            try
            {
                string msgId = _mw.GenerateId(25);
                await _mw.ApiCallAsync(
                    "INSERT INTO uzenetek(id,sender_id,receiver_id,message) VALUES(@p0,@p1,@p2,@p3)",
                    new object[] { msgId, MainWindow.LoggedInUserId, _partnerId.Value, _msgInput.Text },
                    "insert");
                _msgInput.Clear();
                await LoadMessagesAsync(_partnerId.Value);
                await LoadPartnersAsync();
            }
            catch (Exception ex) { MessageBox.Show("Hiba: " + ex.Message); }
        }
    }

    // ============================================
    // VÁSÁRLÁS OLDAL
    // ============================================
    public class PurchasePage : Page
    {
        private readonly MainWindow _mw;
        private readonly string _itemId;
        private ItemViewModel _item;
        private TextBox _nameBox, _emailBox, _phoneBox, _zipBox, _cityBox, _addrBox;
        private TextBlock _titleLabel, _priceLabel;

        public PurchasePage(MainWindow mw, string itemId)
        {
            _mw = mw;
            _itemId = itemId;
            Build();
            Loaded += async (s, e) => await LoadItemAsync();
        }

        private void Build()
        {
            Background = new SolidColorBrush(Color.FromRgb(0x05, 0x05, 0x05));
            var sv = new ScrollViewer();
            var stack = new StackPanel { Margin = new Thickness(40) };

            var backBtn = MainWindow.MakeGhostButton("← Vissza");
            backBtn.HorizontalAlignment = HorizontalAlignment.Left;
            backBtn.Click += (s, e) => _mw.NavigateToMain();
            stack.Children.Add(backBtn);

            stack.Children.Add(new TextBlock
            {
                Text = "🛒 Vásárlás",
                FontSize = 22,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x9A, 0x1F)),
                Margin = new Thickness(0, 16, 0, 16)
            });

            var infoBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x0F, 0x0F, 0x0F)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x29, 0xFF, 0x8C, 0x00)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(20, 16, 20, 16),
                Margin = new Thickness(0, 0, 0, 24)
            };
            var infoStack = new StackPanel();
            _titleLabel = new TextBlock
            {
                Text = "Betöltés...",
                Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x9A, 0x1F)),
                FontSize = 18,
                FontWeight = FontWeights.Bold
            };
            _priceLabel = new TextBlock
            {
                Foreground = new SolidColorBrush(Color.FromRgb(0xF5, 0xF0, 0xE8)),
                FontSize = 16,
                Margin = new Thickness(0, 4, 0, 0)
            };
            infoStack.Children.Add(_titleLabel);
            infoStack.Children.Add(_priceLabel);
            infoBorder.Child = infoStack;
            stack.Children.Add(infoBorder);

            stack.Children.Add(new TextBlock
            {
                Text = "Szállítási adatok",
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(0xF5, 0xF0, 0xE8)),
                Margin = new Thickness(0, 0, 0, 16)
            });

            var formGrid = new Grid();
            formGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            formGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            _nameBox = AddFormField(formGrid, "Teljes név *", 0, 0, true);
            _emailBox = AddFormField(formGrid, "Email *", 1, 0, false);
            _phoneBox = AddFormField(formGrid, "Telefonszám *", 1, 1, false);
            _zipBox = AddFormField(formGrid, "Irányítószám *", 2, 0, false);
            _cityBox = AddFormField(formGrid, "Város *", 2, 1, false);
            _addrBox = AddFormField(formGrid, "Cím *", 3, 0, true);

            while (formGrid.RowDefinitions.Count < 5)
                formGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var submitBtn = MainWindow.MakeOrangeButton("Rendelés leadása", 15);
            submitBtn.Margin = new Thickness(5, 16, 5, 0);
            Grid.SetRow(submitBtn, 4);
            Grid.SetColumnSpan(submitBtn, 2);
            formGrid.Children.Add(submitBtn);
            submitBtn.Click += async (s2, e2) => await SubmitAsync();

            stack.Children.Add(formGrid);
            sv.Content = stack;
            Content = sv;
        }

        private TextBox AddFormField(Grid grid, string label, int row, int col, bool fullWidth)
        {
            while (grid.RowDefinitions.Count <= row)
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var panel = new StackPanel { Margin = new Thickness(5, 5, 5, 5) };
            panel.Children.Add(MainWindow.MakeLabel(label));
            var tb = MainWindow.MakeInput();
            panel.Children.Add(tb);

            Grid.SetRow(panel, row);
            Grid.SetColumn(panel, col);
            if (fullWidth) Grid.SetColumnSpan(panel, 2);
            grid.Children.Add(panel);
            return tb;
        }

        private async Task LoadItemAsync()
        {
            try
            {
                var items = await _mw.ApiSelectAsync<Dictionary<string, JsonElement>>(
                    @"SELECT i.id, i.title, i.price, i.sold, i.user_id, u.username
                      FROM items i JOIN users u ON i.user_id=u.id
                      WHERE i.id=@p0",
                    new object[] { _itemId });

                if (items.Count > 0)
                {
                    var r = items[0];
                    _item = new ItemViewModel
                    {
                        Id = r["id"].ToString(),
                        Title = r["title"].ToString(),
                        Price = decimal.Parse(r["price"].ToString(), System.Globalization.CultureInfo.InvariantCulture),
                        IsSold = r["sold"].ToString() == "1",
                        SellerId = int.Parse(r["user_id"].ToString()),
                        SellerName = r["username"].ToString()
                    };
                    _titleLabel.Text = _item.Title;
                    _priceLabel.Text = _item.PriceFormatted;
                }
            }
            catch { }
        }

        private async Task SubmitAsync()
        {
            if (!_mw.CanPerformWriteOperation())
            { MessageBox.Show("VIZSGALOCK aktív!"); return; }
            if (_item == null)
            { MessageBox.Show("A termék nem található!"); return; }
            if (_item.IsSold)
            { MessageBox.Show("Ez a termék már elkelt!"); return; }
            if (_item.SellerId == MainWindow.LoggedInUserId)
            { MessageBox.Show("Nem vásárolhatod meg a saját termékedet!"); return; }

            string name = _nameBox.Text.Trim();
            string email = _emailBox.Text.Trim();
            string phone = _phoneBox.Text.Trim();
            string zip = _zipBox.Text.Trim();
            string city = _cityBox.Text.Trim();
            string addr = _addrBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(phone) || string.IsNullOrWhiteSpace(zip) ||
                string.IsNullOrWhiteSpace(city) || string.IsNullOrWhiteSpace(addr))
            { MessageBox.Show("Minden mező kitöltése kötelező!"); return; }

            try
            {
                string orderId = _mw.GenerateId();
                await _mw.ApiCallAsync(
                    @"INSERT INTO orders(id,buyer_id,seller_id,item_id,status,
                        shipping_name,shipping_email,shipping_phone,
                        shipping_zip,shipping_city,shipping_address,payment_method)
                      VALUES(@p0,@p1,@p2,@p3,'pending',
                        @p4,@p5,@p6,@p7,@p8,@p9,'cod')",
                    new object[] { orderId, MainWindow.LoggedInUserId, _item.SellerId, _itemId, name, email, phone, zip, city, addr },
                    "insert");

                await _mw.ApiExecuteAsync(
                    "UPDATE items SET sold=1 WHERE id=@p0",
                    new object[] { _itemId });

                MessageBox.Show("Rendelés leadva!", "Siker", MessageBoxButton.OK, MessageBoxImage.Information);
                _mw.NavigateToMain();
            }
            catch (Exception ex) { MessageBox.Show("Hiba: " + ex.Message); }
        }
    }
}