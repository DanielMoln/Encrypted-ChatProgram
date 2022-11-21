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
			// A form bet�lt�sekor lefut� taskok ind�t�sa, amik k�l�n sz�lon fognak futni
			Task.Run(async () =>
			{
				// Innent�l egy m�sik sz�lon vagyunk!!!

				TcpListener listener = new TcpListener(IPAddress.Any, 2546);
				listener.Start();
				listBox1.Invoke(() =>
					{
						listBox1.Items.Add($"Listening on port: 2546");
					});
				while (true)
				{
					var client = listener.AcceptTcpClient();
					// A kliens kapcsolatot kirakjuk egy m�sik v�rakoz�s n�lk�li task-ra az�rt, hogy az AcceptTcpClient() met�dus tudja fogadni a tov�bbi bej�v� tcp k�r�seket.
					Task.Run(async () =>
					{
						clients.Add(new ChatClient()
						{
							IPAddress = client.Client.RemoteEndPoint.ToString()
						});

						// Lek�rj�k a h�l�zati kapcsolathoz tartoz� stream-et
						NetworkStream strm = client.GetStream();
						// A kliensnek visszak�ldj�k a sikeres kapcsol�d�si �zenetet

						byte[] sendingdata = Encoding.UTF8.GetBytes($"Connected to server {client.Client.LocalEndPoint.ToString()}");
						sendingdata = Encryption.EncryptAesCBC(sendingdata, encryptionKey);

						await strm.WriteAsync(sendingdata);
						byte[] readBuffer = new byte[2048];
						string data = "";
						int i;
						// K�l�n taskra kell helyezni v�rakoz�s n�lk�l (await n�lk�l) a bej�v� adatok olvas�s�t a stream-b�l, mivel az strm.Read blokkolja a tov�bbi m�veleteket, �gy azok nem hajt�dn�nak v�gre
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
										// A bej�v� �zeneteket belehelyezz�k a SortedDictionary-be
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
						}).GetAwaiter(); // A GetAwaiter()-rel h�vva a task nem fog v�rakozni, hanem kihelyez�dik a h�tt�rbe, a fut�s pedig megy tov�bb

						// K�l�n task-ra helyezz�k a bej�v� �zenetek feldolgoz�s�st, �s a m�g nem kik�ld�ttek kik�ld�s�t a felhaszn�l�k sz�m�ra. A task-nak innen nem kell tov�bbmennie, emiatt await-tel h�vjuk meg GetAwaiter() n�lk�l. �gy a fut�s itt v�rakozik eg�szen addig, am�g a Task nem fejez�dik be (vagy a kapcsolat nem szakad meg).
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
								// A task fut�s�n�l 500 ms-os delay-t �ll�tunk be (de igaz�b�l lehetne j�val kevesebb is), k�l�nben a processzorhaszn�lata a tasknak drasztikusan megemelkedik. Ezzel a k�sleltet�ssel a processzorhaszn�lat gyakorlatilag 0.
								await Task.Delay(500);
							}
						});
						strm.Close();
						client.Close();
					}).GetAwaiter();
				}
				listener.Stop();
			}).GetAwaiter();

			// A kliens oldalon a h�l�zati kapcsolatot kirakjuk egy v�rakoz�s n�lk�li Task-ra az�rt, hogy a kapcsolat fel�p�t�s �s folyamatos haszn�lat ne befoly�solja a Form bet�lt�s�t.
			Task.Run(async () =>
					{
						TcpClient client = new TcpClient();
						await client.ConnectAsync("127.0.0.1", 2546); // a c�mzett ip c�me nyilv�n �t�rhat�. 
						NetworkStream strm = client.GetStream();
						byte[] readBuffer = new byte[2048];
						int i = 0;
						// A network stream-b�l olvas�st szint�n kirakjuk egy v�rakoz�s n�lk�li Task-ra, ugyanis az strm.Read met�dus blokkolja a sz�l fut�s�t
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
												// Mivel a listbox elem�nk a f� Task-on (f� sz�lon) fut, emiatt egy h�tt�rben fut� Task-b�l nem tudunk hozz�f�rni. Minden Form elemnek van egy Invoke nev� met�dusa, amivel h�tt�rsz�lb�l lehets�ges hozz�f�r�st k�rni az objektumhoz
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

						// V�gtelen ciklust ind�tunk (legal�bbis am�g a client.Connected true), �s figyelj�nk, hogy a felhaszn�l� �rt-e �jabb hozz�sz�l�st. Ha igen, akkor elk�ldj�k (bele�rjuk a stream-be), majd null-ra �ll�tjuk az �zenet objektumot
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
							// A task fut�s�n�l 10 ms-os delay-t �ll�tunk be, k�l�nben a processzorhaszn�lata a tasknak drasztikusan megemelkedik. Ezzel a 10 ms k�sleltet�ssel a processzorhaszn�lat gyakorlatilag 0.
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