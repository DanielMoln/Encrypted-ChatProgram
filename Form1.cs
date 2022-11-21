using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Security.Cryptography.GenericEncryption;

namespace ChatProgram
{
	public partial class Form1 : Form
	{
		SortedDictionary<DateTime, ClientMessage> messages = new SortedDictionary<DateTime, ClientMessage>();
		List<ChatClient> clients = new List<ChatClient>();
		ClientMessage currentMessage = null;
		string encryptionKey = "asdgasd3lklj23ljfh2ou3hro28f48hh4fhl8chjvhjw4848483ldj//!!++";

		public Form1()
		{
			InitializeComponent();
		}

		private async void Form1_Load(object sender, EventArgs e)
		{
			// A form betöltésekor lefutó taskok indítása, amik külön szálon fognak futni
			Task.Run(async () =>
			{
				// Innentõl egy másik szálon vagyunk!!!

				TcpListener listener = new TcpListener(IPAddress.Any, 2546);
				listener.Start();
				listBox1.Invoke(() =>
					{
						listBox1.Items.Add($"Listening on port: 2546");
					});
				while (true)
				{
					var client = listener.AcceptTcpClient();
					// A kliens kapcsolatot kirakjuk egy másik várakozás nélküli task-ra azért, hogy az AcceptTcpClient() metódus tudja fogadni a további bejövö tcp kéréseket.
					Task.Run(async () =>
					{
						clients.Add(new ChatClient()
						{
							IPAddress = client.Client.RemoteEndPoint.ToString()
						});

						// Lekérjük a hálózati kapcsolathoz tartozó stream-et
						NetworkStream strm = client.GetStream();
						// A kliensnek visszaküldjük a sikeres kapcsolódási üzenetet

						byte[] sendingdata = Encoding.UTF8.GetBytes($"Connected to server {client.Client.LocalEndPoint.ToString()}");
						sendingdata = Encryption.EncryptAesCBC(sendingdata, encryptionKey);

						await strm.WriteAsync(sendingdata);
						byte[] readBuffer = new byte[2048];
						string data = "";
						int i;
						// Külön taskra kell helyezni várakozás nélkül (await nélkül) a bejövõ adatok olvasását a stream-bõl, mivel az strm.Read blokkolja a további mûveleteket, így azok nem hajtódnának végre
						Task.Run(async () =>
						{
							while ((i = strm.Read(readBuffer, 0, readBuffer.Length)) != 0)
							{
								try
								{
									byte[] decrypted = Encryption.DecryptAesCBC(_extractData(readBuffer), encryptionKey);
									data = Encoding.UTF8.GetString(decrypted);
									if (data.Length > 0)
									{
										// A bejövõ üzeneteket belehelyezzük a SortedDictionary-be
										messages.Add(DateTime.Now, new ClientMessage()
										{
											Message = $"{client.Client.RemoteEndPoint.ToString()} ({DateTime.Now}) : {data}",
										});
										readBuffer = new byte[2048];
										GC.Collect();
									}
								}
								catch (Exception e)
								{
									MessageBox.Show(e.Message);
								}
							}
						}).GetAwaiter(); // A GetAwaiter()-rel hívva a task nem fog várakozni, hanem kihelyezõdik a háttérbe, a futás pedig megy tovább

						// Külön task-ra helyezzük a bejövõ üzenetek feldolgozásást, és a még nem kiküldöttek kiküldését a felhasználók számára. A task-nak innen nem kell továbbmennie, emiatt await-tel hívjuk meg GetAwaiter() nélkül. Így a futás itt várakozik egészen addig, amíg a Task nem fejezõdik be (vagy a kapcsolat nem szakad meg).
						await Task.Run(async () =>
						{
							while (true)
							{
								foreach (var message in messages)
								{
									if (!clients.Single(a => a.IPAddress == client.Client.RemoteEndPoint.ToString()).MessageIds.Any(a => a == message.Value.Id))
									{
										byte[] encrypted = Encryption.EncryptAesCBC(Encoding.UTF8.GetBytes(message.Value.Message), encryptionKey);
										await strm.WriteAsync(encrypted);
										clients.Single(a => a.IPAddress == client.Client.RemoteEndPoint.ToString()).MessageIds.Add(message.Value.Id);
									}
								}
								// A task futásánál 500 ms-os delay-t állítunk be (de igazából lehetne jóval kevesebb is), különben a processzorhasználata a tasknak drasztikusan megemelkedik. Ezzel a késleltetéssel a processzorhasználat gyakorlatilag 0.
								await Task.Delay(500);
							}
						});
						strm.Close();
						client.Close();
					}).GetAwaiter();
				}
				listener.Stop();
			}).GetAwaiter();

			// A kliens oldalon a hálózati kapcsolatot kirakjuk egy várakozás nélküli Task-ra azért, hogy a kapcsolat felépítés és folyamatos használat ne befolyásolja a Form betöltését.
			Task.Run(async () =>
					{
						TcpClient client = new TcpClient();
						await client.ConnectAsync("127.0.0.1", 2546); // a címzett ip címe nyilván átírható. 
						NetworkStream strm = client.GetStream();
						byte[] readBuffer = new byte[2048];
						int i = 0;
						// A network stream-bõl olvasást szintén kirakjuk egy várakozás nélküli Task-ra, ugyanis az strm.Read metódus blokkolja a szál futását
						Task.Run(async () =>
								{
									while ((i = strm.Read(readBuffer)) != 0)
									{
										if (readBuffer[0] != 0)
										{
											try
											{
												byte[] encrypted = _extractData(readBuffer);
												string data = Encoding.UTF8.GetString(Encryption.DecryptAesCBC(encrypted, encryptionKey));
												// Mivel a listbox elemünk a fõ Task-on (fõ szálon) fut, emiatt egy háttérben futó Task-ból nem tudunk hozzáférni. Minden Form elemnek van egy Invoke nevû metódusa, amivel háttérszálból lehetséges hozzáférést kérni az objektumhoz
												listBox1.Invoke(() =>
												{
													listBox1.Items.Add($"{data}");
												});
											}
											catch (Exception e)
											{
												listBox1.Invoke(() =>
												{
													listBox1.Items.Add($"{e.Message}");
												});
											}
										}
										readBuffer = new byte[2048];
										GC.Collect();
									}
								}).GetAwaiter();

						// Végtelen ciklust indítunk (legalábbis amíg a client.Connected true), és figyeljünk, hogy a felhasználó írt-e újabb hozzászólást. Ha igen, akkor elküldjük (beleírjuk a stream-be), majd null-ra állítjuk az üzenet objektumot
						while (client.Connected)
						{
							if (currentMessage != null)
							{
								try
								{
									byte[] data = Encryption.EncryptAesCBC(Encoding.UTF8.GetBytes(currentMessage.Message), encryptionKey);
									await strm.WriteAsync(data);
									currentMessage = null;
								}
								catch (Exception e)
								{

								}
							}
							// A task futásánál 10 ms-os delay-t állítunk be, különben a processzorhasználata a tasknak drasztikusan megemelkedik. Ezzel a 10 ms késleltetéssel a processzorhasználat gyakorlatilag 0.
							await Task.Delay(10);
						}

						client.Close();
					}).GetAwaiter();
		}

		private byte[] _extractData(byte[] input)
		{
			try
			{
				for (int i = 0; i < input.Length; i += 16)
				{
					if (input[i] == 0)
					{
						return input[0..(i)];
					}
				}
			}
			catch
			{
			}
			return input;
		}

		private void button1_Click(object sender, EventArgs e)
		{
			this.currentMessage = new ClientMessage()
			{
				Message = this.textBox2.Text
			};
		}
	}
}