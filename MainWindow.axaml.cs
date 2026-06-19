using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using DiscordRPC;

using AvaloniaButton = Avalonia.Controls.Button;
using DiscordButton = DiscordRPC.Button;

namespace VarinaDiscordTool
{
    public partial class MainWindow : Window
    {
        private static readonly HttpClient client = new HttpClient();
        private DiscordRpcClient? discord;
        private System.Timers.Timer? rpcTimer;

        private static readonly string PresetsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "VarinaTool", "presets.txt");

        public MainWindow()
        {
            InitializeComponent();
            InitRichPresence();
            LoadPresets();
        }

        // ================= RPC =================
        private void InitRichPresence()
        {
            try
            {
                discord = new DiscordRpcClient("1498034553485267065");
                discord.Logger = new DiscordRPC.Logging.ConsoleLogger()
                {
                    Level = DiscordRPC.Logging.LogLevel.Warning
                };
                discord.Initialize();

                UpdateRichPresence();

                rpcTimer = new System.Timers.Timer(15000);
                rpcTimer.Elapsed += (_, _) => Dispatcher.UIThread.Post(UpdateRichPresence);
                rpcTimer.AutoReset = true;
                rpcTimer.Start();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"RPC init failed: {ex.Message}");
            }
        }

        private void UpdateRichPresence()
        {
            if (discord == null) return;

            try
            {
                discord.SetPresence(new RichPresence()
                {
                    Details = "Building Discord tools",
                    State = "Using NexText App",
                    Timestamps = Timestamps.Now,
                    Assets = new Assets()
                    {
                        LargeImageKey = "nextext",
                        LargeImageText = "NexText Discord Tool"
                        SmallImageKey = "oathstudios"
                    },
                    Buttons = new DiscordButton[]
                    {
                        new DiscordButton() { Label = "Join Discord", Url = "https://discord.gg/wBVxJbjdcZ" },
                        new DiscordButton() { Label = "Website", Url = "https://oathstudios.org" }
                    }
                });
            }
            catch { /* RPC is optional */ }
        }

        protected override void OnClosed(EventArgs e)
        {
            rpcTimer?.Stop();
            rpcTimer?.Dispose();
            discord?.Dispose();
            base.OnClosed(e);
        }

        // ================= NAV =================
        private void HideAll()
        {
            WebhookPanel.IsVisible = false;
            EmbedPanel.IsVisible = false;
            ServerPanel.IsVisible = false;
            ChannelPanel.IsVisible = false;
        }

        private void ShowWebhook(object sender, RoutedEventArgs e) { HideAll(); WebhookPanel.IsVisible = true; }
        private void ShowEmbed(object sender, RoutedEventArgs e) { HideAll(); EmbedPanel.IsVisible = true; }
        private void ShowServer(object sender, RoutedEventArgs e) { HideAll(); ServerPanel.IsVisible = true; }
        private void ShowChannels(object sender, RoutedEventArgs e) { HideAll(); ChannelPanel.IsVisible = true; }

        // ================= WEBHOOK =================
        private async void BtnSendWebhook_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtWebhookUrl.Text))
            {
                TxtWebhookOutput.Text = "Please enter a webhook URL.";
                return;
            }

            var btn = sender as AvaloniaButton;
            if (btn != null) btn.IsEnabled = false;

            try
            {
                var payload = new
                {
                    content = TxtWebhookMessage.Text?.Trim() ?? "",
                    embeds = string.IsNullOrWhiteSpace(TxtImageUrl.Text)
                        ? null
                        : new[]
                        {
                            new { image = new { url = TxtImageUrl.Text.Trim() } }
                        }
                };

                var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
                {
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                });

                var response = await client.PostAsync(
                    TxtWebhookUrl.Text.Trim(),
                    new StringContent(json, Encoding.UTF8, "application/json"));

                TxtWebhookOutput.Text = response.IsSuccessStatusCode
                    ? "✅ Message sent successfully!"
                    : $"❌ Failed: {(int)response.StatusCode} {response.ReasonPhrase}";
            }
            catch (Exception ex)
            {
                TxtWebhookOutput.Text = $"❌ Error: {ex.Message}";
            }
            finally
            {
                if (btn != null) btn.IsEnabled = true;
            }
        }

        // ================= EMBED =================
        private async void BtnSendEmbed_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtWebhookUrl.Text))
            {
                TxtWebhookOutput.Text = "Please enter a webhook URL.";
                return;
            }

            var btn = sender as AvaloniaButton;
            if (btn != null) btn.IsEnabled = false;

            try
            {
                int color = 0;
                string colorStr = TxtEmbedColor.Text?.Trim().TrimStart('#') ?? "";
                if (!string.IsNullOrEmpty(colorStr))
                    int.TryParse(colorStr, System.Globalization.NumberStyles.HexNumber, null, out color);

                var embed = new
                {
                    title = TxtEmbedTitle.Text?.Trim() ?? "",
                    description = TxtEmbedDesc.Text?.Trim() ?? "",
                    color = color,
                    image = string.IsNullOrWhiteSpace(TxtEmbedImage.Text)
                        ? null
                        : new { url = TxtEmbedImage.Text.Trim() }
                };

                var payload = new { embeds = new[] { embed } };
                var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
                {
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                });

                var response = await client.PostAsync(
                    TxtWebhookUrl.Text.Trim(),
                    new StringContent(json, Encoding.UTF8, "application/json"));

                TxtWebhookOutput.Text = response.IsSuccessStatusCode
                    ? "✅ Embed sent successfully!"
                    : $"❌ Failed: {(int)response.StatusCode}";
            }
            catch (Exception ex)
            {
                TxtWebhookOutput.Text = $"❌ Error sending embed: {ex.Message}";
            }
            finally
            {
                if (btn != null) btn.IsEnabled = true;
            }
        }

        // ================= SERVER VIEWER =================
        private async void BtnServerViewer_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtViewerToken.Text) || string.IsNullOrWhiteSpace(TxtViewerGuildId.Text))
            {
                TxtViewerOutput.Text = "Please provide both Bot Token and Guild ID.";
                return;
            }

            string guildId = TxtViewerGuildId.Text.Trim();
            string token = TxtViewerToken.Text.Trim();

            try
            {
                string guildJson, channelsJson;

                using (var req = new HttpRequestMessage(HttpMethod.Get,
                    $"https://discord.com/api/v10/guilds/{guildId}?with_counts=true"))
                {
                    req.Headers.Add("Authorization", $"Bot {token}");
                    var res = await client.SendAsync(req);
                    res.EnsureSuccessStatusCode();
                    guildJson = await res.Content.ReadAsStringAsync();
                }

                using (var req = new HttpRequestMessage(HttpMethod.Get,
                    $"https://discord.com/api/v10/guilds/{guildId}/channels"))
                {
                    req.Headers.Add("Authorization", $"Bot {token}");
                    var res = await client.SendAsync(req);
                    res.EnsureSuccessStatusCode();
                    channelsJson = await res.Content.ReadAsStringAsync();
                }

                using var gDoc = JsonDocument.Parse(guildJson);
                using var cDoc = JsonDocument.Parse(channelsJson);

                var root = gDoc.RootElement;
                int members = root.TryGetProperty("approximate_member_count", out var m) ? m.GetInt32() : 0;
                int online = root.TryGetProperty("approximate_presence_count", out var o) ? o.GetInt32() : 0;

                TxtViewerOutput.Text = $"✅ Members: {members}\nOnline: {online}";

                DrawGraph(members, online);
                LoadChannels(cDoc);
            }
            catch (Exception ex)
            {
                TxtViewerOutput.Text = $"❌ Failed to load server: {ex.Message}";
            }
        }

        // ================= CHANNELS =================
        private void LoadChannels(JsonDocument doc)
        {
            ChannelList.Items.Clear();

            foreach (var ch in doc.RootElement.EnumerateArray())
            {
                if (!ch.TryGetProperty("name", out var nameProp) || !ch.TryGetProperty("type", out var typeProp))
                    continue;

                string name = nameProp.GetString() ?? "unknown";
                int type = typeProp.GetInt32();

                string prefix = type switch
                {
                    0 => "# ",
                    2 => "🔊 ",
                    4 => "📁 ",
                    _ => ""
                };

                ChannelList.Items.Add($"{prefix}{name}");
            }
        }

        // ================= GRAPH =================
        private void DrawGraph(int members, int online)
        {
            GraphPanel.Children.Clear();

            int maxValue = Math.Max(members, online);
            if (maxValue == 0) maxValue = 1;

            AddBar("Members", members, maxValue);
            AddBar("Online", online, maxValue);
        }

        private void AddBar(string label, int value, int maxValue)
        {
            double normalizedHeight = maxValue > 0 ? (double)value / maxValue * 300 : 10;

            var stack = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Margin = new Thickness(15, 0),
                HorizontalAlignment = HorizontalAlignment.Center
            };

            stack.Children.Add(new Border
            {
                Width = 50,
                Height = Math.Max(20, normalizedHeight),
                Background = Brushes.Cyan,
                CornerRadius = new CornerRadius(4)
            });

            stack.Children.Add(new TextBlock
            {
                Text = $"{label}\n{value}",
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 8, 0, 0)
            });

            GraphPanel.Children.Add(stack);
        }

        // ================= PRESETS =================
        private void BtnSavePreset_Click(object sender, RoutedEventArgs e)
        {
            string url = TxtWebhookUrl.Text?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(url)) return;

            if (PresetDropdown.Items.Contains(url))
            {
                TxtWebhookOutput.Text = "Preset already exists.";
                return;
            }

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(PresetsPath)!);
                File.AppendAllText(PresetsPath, url + Environment.NewLine);
                PresetDropdown.Items.Add(url);
                TxtWebhookOutput.Text = "✅ Preset saved.";
            }
            catch (Exception ex)
            {
                TxtWebhookOutput.Text = $"❌ Failed to save preset: {ex.Message}";
            }
        }

        private void PresetChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PresetDropdown.SelectedItem is string selectedUrl)
                TxtWebhookUrl.Text = selectedUrl;
        }

        private void LoadPresets()
        {
            if (!File.Exists(PresetsPath)) return;

            foreach (var line in File.ReadAllLines(PresetsPath))
            {
                string trimmed = line.Trim();
                if (!string.IsNullOrEmpty(trimmed) && !PresetDropdown.Items.Contains(trimmed))
                    PresetDropdown.Items.Add(trimmed);
            }
        }
    }
}
