using System;
using System.IO;

using FellowOakDicom;

namespace ProfileEvaluation
{
	public class DicomReader
	{
		private static double[,] CastElementToDouble<T>(T[,] @this)
		{
			var newArr = new double[@this.GetLength(0), @this.GetLength(1)];
			for (int x = 0; x < @this.GetLength(0); ++x)
				for (int y = 0; y < @this.GetLength(1); ++y)
					newArr[x, y] = Convert.ToDouble(@this[x, y]);
			return newArr;
		}

		private static double[,,] CastElementToDouble<T>(T[,,] @this)
		{
			var newArr = new double[@this.GetLength(0), @this.GetLength(1), @this.GetLength(2)];
			for (int x = 0; x < @this.GetLength(0); ++x)
				for (int y = 0; y < @this.GetLength(1); ++y)
					for (int z = 0; z < @this.GetLength(2); ++z)
						newArr[x, y, z] = Convert.ToDouble(@this[x, y, z]);
			return newArr;
		}

		private static T[,,] AssignToFrame<T>(T[,,] @this, int frame, T[,] plane)
		{
			for (int x = 0; x < @this.GetLength(1); ++x)
				for (int y = 0; y < @this.GetLength(2); ++y)
					@this[frame, x, y] = plane[x, y];
			return @this;
		}

		public static T[,] PixelDataToArray2D<T>(T[] pixelData, int rows, int columns)
		{
			// Validate the data length
			if (pixelData.Length != rows * columns)
				throw new InvalidOperationException("PixelData length does not match the expected dimensions.");

			// 2D Dose Grid
			T[,] pixelMatrix = new T[rows, columns];
			for (int i = 0; i < rows; ++i)
			{
				for (int j = 0; j < columns; ++j)
				{
					int index = i * columns + j;
					pixelMatrix[i, j] = pixelData[index];
				}
			}
			return pixelMatrix;
		}

		public static T[,,] PixelDataToArray3D<T>(T[] pixelData, int rows, int columns, int frames)
		{
			// Validate the data length
			if (pixelData.Length != rows * columns * frames)
				throw new InvalidOperationException("PixelData length does not match the expected dimensions.");

			// 3D Dose Grid
			T[,,] pixelMatrix = new T[frames, rows, columns];
			for (int f = 0; f < frames; ++f)
			{
				for (int i = 0; i < rows; ++i)
				{
					for (int j = 0; j < columns; ++j)
					{
						int index = f * (rows * columns) + i * columns + j;
						pixelMatrix[f, i, j] = pixelData[index];
					}
				}
			}
			return pixelMatrix;
		}

		public static double[,,] ReadDoseMatrix(DicomFile dicom)
		{
			var bitsAllocated = dicom.Dataset.GetSingleValue<ushort>(DicomTag.BitsAllocated);
			// shall be 1 or , 8*n

			if (!dicom.Dataset.Contains(DicomTag.PixelData))
				throw new ArgumentException($"DICOM file does not contain pixel data ({DicomTag.PixelData}).");

			if (!string.Equals(dicom.FileMetaInfo.MediaStorageSOPClassUID.Name, "RT Dose Storage", StringComparison.OrdinalIgnoreCase))
				throw new ArgumentException($"DICOM file is not 'RT Dose Storage' but `{dicom.FileMetaInfo.MediaStorageSOPClassUID.Name}`!");

			// Get dose scaling
			double doseGridScaling = dicom.Dataset.GetSingleValueOrDefault(DicomTag.DoseGridScaling, 1.0);

			// Get DICOM dimensions
			int rows = dicom.Dataset.GetSingleValue<int>(DicomTag.Rows);
			int columns = dicom.Dataset.GetSingleValue<int>(DicomTag.Columns);
			int frames = dicom.Dataset.TryGetSingleValue(DicomTag.NumberOfFrames, out int nFrames) ? nFrames : 1; // Depth (default to 1 if not 3D)

			double[,,] doseMatrix = new double[frames, rows, columns];

			if (dicom.Dataset.TryGetValues(DicomTag.PixelData, out ushort[] data16Bit))
			{
				if (bitsAllocated < 16)
					throw new ArgumentException($"PixelData is 16-bit but only {bitsAllocated} were allocated? Makes no sense at all!");
				if (bitsAllocated == 16)
				{
					if (frames == 1)
					{
						var dosePlane = CastElementToDouble(PixelDataToArray2D(data16Bit, rows, columns));
						AssignToFrame(doseMatrix, 0, dosePlane);
					}
					else
						doseMatrix = CastElementToDouble(PixelDataToArray3D(data16Bit, rows, columns, frames));
				}
				else if (bitsAllocated == 32)
				{
					var data32Bit = new uint[data16Bit.Length / 2];
					for (int i = 0; i < data32Bit.Length; ++i)
						data32Bit[i] = (uint)(data16Bit[2 * i] | (data16Bit[2 * i + 1] << 16));

					if (frames == 1)
					{
						var dosePlane = CastElementToDouble(PixelDataToArray2D(data32Bit, rows, columns));
						AssignToFrame(doseMatrix, 0, dosePlane);
					}
					else
						doseMatrix = CastElementToDouble(PixelDataToArray3D(data32Bit, rows, columns, frames));
				}
				else
					throw new NotImplementedException($"16-bit to {bitsAllocated}-bit");
			}
			else if (dicom.Dataset.TryGetValues(DicomTag.PixelData, out byte[] data8Bit)) // must be 8-bit (OB)
			{
				if (bitsAllocated < 8)
					throw new ArgumentException($"PixelData is 8-bit but only {bitsAllocated} were allocated? Makes no sense at all!");
				throw new NotImplementedException("OB (8-bit) pixel data is not yet supported.");
			}
			else
				throw new ArrayTypeMismatchException("PixelData appears to be neither 8-bit nor 16-bit!");

			for (int x = 0; x < doseMatrix.GetLength(0); ++x)
				for (int y = 0; y < doseMatrix.GetLength(1); ++y)
					for (int z = 0; z < doseMatrix.GetLength(2); ++z)
						doseMatrix[x, y, z] = doseMatrix[x, y, z] * doseGridScaling;

			return doseMatrix;
		}


		//private Stream dicomStream = null;
		private DicomFile dicomFile = null;
		public DicomFile DicomFile
		{
			get
			{
				if (dicomFile is null)
				{
					dicomFile = DicomFile.Open(dicomStream, FileReadOption.ReadAll);
					dicomStream.Close();
				}
				return dicomFile;
			}
			private set => dicomFile = value;
		}
		private Stream dicomStream = null;

		public DicomReader(string filePath)
			: this(File.OpenRead(filePath))
		{ }

		public DicomReader(FileStream dicomStream)
		{
			this.dicomStream = dicomStream ?? throw new ArgumentNullException(nameof(dicomStream));
		}

		public double[,,] ReadDoseMatrix() => ReadDoseMatrix(DicomFile);

		public double[] GetPixelSpacing() => DicomFile?.Dataset?.GetValues<double>(DicomTag.PixelSpacing);
	}
}
