using ManagedBass;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Serialization;

namespace MinimalAudioWaveformGenerator
{
	class Program
	{
		private const string VERSION_NUMBER = "1.0.0";
		private const string RELEASE_DATE = "2021-02-02";

		private static WaveformParameters lastParameters;
		private static string lastParamsLocation = Environment.CurrentDirectory + "\\last_params.xml";

		private static bool finishedDrawing = false;
		static void Main(string[] args)
		{
			AppDomain.CurrentDomain.ProcessExit += OnProcessExit;

			if (!Bass.Init())
			{
				Console.WriteLine("Couldn't initialize audio device. Press any key to exit.");
				Console.ReadKey(true);
				Environment.Exit(-1);
			}
			ReadLastParams();
			if (args.Length > 1)
			{
				RunGenerator(args[0]);
			}
			while (true)
			{
				if (finishedDrawing)
				{
					finishedDrawing = false;
					string answer = "";
					while (answer != "y" && answer != "n")
					{
						Console.WriteLine("Do you wish to draw a waveform using the same audio file again? (y/n)");
						answer = Console.ReadLine();
					}
					if (answer == "y")
						RunGenerator(lastParameters.AudioLocation);
				}
				else
				{
					HighlightColors();
					Console.WriteLine("Minimal Audio Waveform Generator (made by PeaQew)");
					Console.WriteLine($"Version {VERSION_NUMBER} {RELEASE_DATE}        ");
					Console.WriteLine($"\nAvailable commands: ");
					Console.WriteLine($"last : show last parameter values that were used\n");
					Console.ResetColor();
					RunGenerator();
				}
			}
		}

		private static string ShowPrompt(string prompt, Type inputType)
		{
			string input = "";
			while (string.IsNullOrEmpty(input))
			{
				if (finishedDrawing)
					break;
				Console.WriteLine(prompt);
				input = Console.ReadLine();
				switch (input)
				{
					case "last":
						ShowLastParams();
						input = "";
						break;
					default:
						bool validation = ValidateInputType(input, inputType);
						if (!validation)
						{
							Console.ForegroundColor = ConsoleColor.Red;
							Console.WriteLine($"Input of type {inputType} expected!");
							Console.ResetColor();
							input = "";
						}
						break;
				}
			}
			return input;
		}

		private static bool ValidateInputType(string input, Type inputType)
		{
			if (inputType == typeof(string))
			{
				return true;
			}
			else if (inputType == typeof(int))
			{
				return int.TryParse(input, out int result);
			}
			else if (inputType == typeof(float))
			{
				return float.TryParse(input, out float result);
			}
			else
			{
				return true;
			}
		}

		private static void RunGenerator(string audioLocation = "")
		{
			if (string.IsNullOrEmpty(audioLocation))
			{
				audioLocation = GetAudioLocation();
			}
			int handle = Bass.CreateStream(audioLocation, 0, 0, BassFlags.Decode | BassFlags.Float);

			while (Bass.LastError != Errors.OK)
			{
				Console.ForegroundColor = ConsoleColor.Red;
				Console.WriteLine("An error occured while creating the audio stream: ");
				Console.WriteLine(Bass.LastError);
				Console.ResetColor();

				audioLocation = GetAudioLocation();
				handle = Bass.CreateStream(audioLocation, 0, 0, BassFlags.Decode | BassFlags.Float);
			}
			HighlightColors();
			Console.WriteLine($" { Bass.LastError } ");
			Console.ResetColor();

			long byteLength = Bass.ChannelGetLength(handle, PositionFlags.Bytes);
			int bitsPerSample = GetBitsPerSample(handle);
			int bytesPerSample = bitsPerSample / 8;
			int lengthInSeconds = (int)Bass.ChannelBytes2Seconds(handle, byteLength);

			Console.ForegroundColor = ConsoleColor.Yellow;
			Console.WriteLine($"Length: {byteLength} bytes ({ lengthInSeconds / 60 }m{ lengthInSeconds % 60 }s)");
			Console.WriteLine($"Bits Per Sample: {bitsPerSample} ({ GetBitsPerSampleString(bitsPerSample) }");
			Console.ResetColor();

			WaveformParameters parameters = new WaveformParameters();
			parameters.AudioLocation = audioLocation;

			parameters.BlockSize = int.Parse(ShowPrompt("Set Block Size: ", typeof(int)));
			parameters.SpaceSize = int.Parse(ShowPrompt("Set Space Size: ", typeof(int)));
			parameters.ImageWidth = int.Parse(ShowPrompt("Set Image Width: ", typeof(int)));
			parameters.PeakHeight = int.Parse(ShowPrompt("Set Peak Height: ", typeof(int)));

			int samplesPerPixel = (int)byteLength / parameters.ImageWidth;
			int stepSize = parameters.BlockSize + parameters.SpaceSize;
			float[] buffer = new float[samplesPerPixel * stepSize];

			Console.WriteLine("Choose a Peak Calculation Method:");
			HighlightColors();
			Console.WriteLine("1: Scaled Average");
			Console.WriteLine("2: Absolute Peak ");
			Console.ResetColor();

			int calculationStrategy = -1;
			while (calculationStrategy < 1 || calculationStrategy > 3)
			{
				calculationStrategy = int.Parse(Console.ReadLine());
				if (calculationStrategy == 1)
				{
					parameters.AverageScale = float.Parse(ShowPrompt("Scale: ", typeof(float)));
				}
				else
				{
					parameters.AverageScale = -1;
				}
			}

			List<float> values = ReadSamples(handle, buffer, calculationStrategy, parameters.AverageScale);

			DrawWaveform(values, parameters);
		}

		private static string GetAudioLocation()
		{
			string audioLocation = "";
			while (!File.Exists(audioLocation))
			{
				audioLocation = ShowPrompt("Specify the location of the audio file (drag and drop -> enter also works): ", typeof(string));
				audioLocation = audioLocation.Replace("\"", string.Empty);
				if (!File.Exists(audioLocation))
				{
					Console.ForegroundColor = ConsoleColor.Red;
					Console.WriteLine("Not a valid file!");
					Console.ResetColor();
				}
			}
			return audioLocation;
		}

		private static int GetBitsPerSample(int handle)
		{
			ChannelInfo info = Bass.ChannelGetInfo(handle);
			return info.Resolution switch
			{
				Resolution.Byte => 8,
				Resolution.Short => 16,
				Resolution.Float => 32,
				_ => -1,
			};
		}

		private static string GetBitsPerSampleString(int resolution)
		{
			return resolution switch
			{
				8 => "Byte",
				16 => "Short",
				32 => "Float",
				_ => "Unknown",
			};
		}

		private static List<float> ReadSamples(int handle, float[] buffer, int strategy, float scale)
		{
			List<float> values = new List<float>();
			int bytesRead = -1;
			int counter = 0;
			switch (strategy)
			{
				case 1: // Scaled Average
					while ((bytesRead = Bass.ChannelGetData(handle, buffer, buffer.Length)) != 0)
					{
						if (bytesRead == -1)
						{
							HighlightColors();
							Console.WriteLine($" { Bass.LastError } ");
							Console.ResetColor();
							break;
						}
						float sum = buffer.Select(s => Math.Abs(s)).Sum();
						values.Add((sum / bytesRead) * scale);

						Console.WriteLine($"At {counter} ({bytesRead} bytes): {values.Last()}");
						counter++;
					}
					break;
				case 2: // Absolute Peak
					while ((bytesRead = Bass.ChannelGetData(handle, buffer, buffer.Length)) != 0)
					{
						if (bytesRead == -1)
						{
							HighlightColors();
							Console.WriteLine($" { Bass.LastError } ");
							Console.ResetColor();
							break;
						}
						values.Add(buffer.Select(x => Math.Abs(x)).Max());

						Console.WriteLine($"At {counter} ({bytesRead} bytes): {values.Last()}");
						counter++;
					}
					break;
			}
			return values;
		}

		private static void DrawWaveform(List<float> values, WaveformParameters parameters)
		{
			Bitmap image = new Bitmap(parameters.ImageWidth, parameters.PeakHeight);
			using (Graphics g = Graphics.FromImage(image))
			{
				int xPos = 0;

				g.SmoothingMode = SmoothingMode.None;

				Brush brush = new SolidBrush(Color.White);
				for (int i = 0; i < values.Count; i++)
				{
					g.FillRectangle(brush, xPos, parameters.PeakHeight - (parameters.PeakHeight * values[i]), parameters.BlockSize, parameters.PeakHeight * values[i]);
					xPos += parameters.BlockSize + parameters.SpaceSize;
				}
				string fileLocation = Environment.CurrentDirectory + "\\Waveform.png";
				image.Save(@fileLocation, ImageFormat.Png);
				HighlightColors();
				Console.WriteLine($"Image saved to { fileLocation }\n");
				Console.ResetColor();
				lastParameters = parameters;
				SaveLastParams();
				finishedDrawing = true;
			}
		}

		private static void ShowLastParams()
		{
			Console.ForegroundColor = ConsoleColor.Yellow;
			if (lastParameters == null)
			{
				Console.WriteLine("There are no previously saved parameters.");
			}
			else
			{
				Console.WriteLine("\nLast parameters:");
				Console.WriteLine($"Block Size: {lastParameters.BlockSize}");
				Console.WriteLine($"Space Size: {lastParameters.SpaceSize}");
				Console.WriteLine($"Image Width: {lastParameters.ImageWidth}");
				Console.WriteLine($"Peak Height: {lastParameters.PeakHeight}");
				if (lastParameters.AverageScale != -1)
				{
					Console.WriteLine($"Peak Calculation Strategy: Average Scale");
					Console.WriteLine($"Scale: {lastParameters.AverageScale}\n");
				}
				else
				{
					Console.WriteLine($"Peak Calculation Strategy: Absolute Peak\n");
				}
			}
			Console.ResetColor();
		}

		private static void ReadLastParams()
		{
			if (File.Exists(lastParamsLocation))
			{
				lastParameters = DeserializeObject<WaveformParameters>(lastParamsLocation);
			}
			else
			{
				lastParameters = null;
			}
		}

		private static void SaveLastParams()
		{
			SerializeObject<WaveformParameters>(lastParameters, lastParamsLocation);
		}

		public static void SerializeObject<T>(T serializableObject, string fileName)
		{
			if (serializableObject == null) { return; }

			try
			{
				XmlDocument xmlDocument = new XmlDocument();
				XmlSerializer serializer = new XmlSerializer(serializableObject.GetType());
				using (MemoryStream stream = new MemoryStream())
				{
					serializer.Serialize(stream, serializableObject);
					stream.Position = 0;
					xmlDocument.Load(stream);
					xmlDocument.Save(fileName);
				}
			}
			catch (Exception ex)
			{
				Console.ForegroundColor = ConsoleColor.Red;
				Console.WriteLine(ex.Message);
				Console.ResetColor();
			}
		}

		public static T DeserializeObject<T>(string fileName)
		{
			if (string.IsNullOrEmpty(fileName)) { return default; }

			T objectOut = default;

			try
			{
				XmlDocument xmlDocument = new XmlDocument();
				xmlDocument.Load(fileName);
				string xmlString = xmlDocument.OuterXml;

				using (StringReader read = new StringReader(xmlString))
				{
					Type outType = typeof(T);

					XmlSerializer serializer = new XmlSerializer(outType);
					using (XmlReader reader = new XmlTextReader(read))
					{
						objectOut = (T)serializer.Deserialize(reader);
					}
				}
			}
			catch (Exception ex)
			{
				Console.ForegroundColor = ConsoleColor.Red;
				Console.WriteLine(ex.Message);
				Console.ResetColor();
			}

			return objectOut;
		}

		private static void HighlightColors()
		{
			Console.BackgroundColor = ConsoleColor.White;
			Console.ForegroundColor = ConsoleColor.Black;
		}

		private static void OnProcessExit(object sender, EventArgs e)
		{
			Bass.Free();
		}
	}
}
