
#region ================== Copyright (c) 2007 Pascal vd Heiden

/*
 * Copyright (c) 2007 Pascal vd Heiden, www.codeimp.com
 * This program is released under GNU General Public License
 * 
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 * 
 */

#endregion

#region ================== Namespaces

using System;
using System.IO;
using System.Drawing;
using CodeImp.DoomBuilder.Data;
using CodeImp.DoomBuilder.Rendering;
using System.Drawing.Imaging;

#endregion

namespace CodeImp.DoomBuilder.IO
{
	internal unsafe class DoomPictureReader : IImageReader
	{
		#region ================== Variables

		// Palette to use
		private readonly Playpal palette;
		
		#endregion

		#region ================== Constructor / Disposer

		// Constructor
		public DoomPictureReader(Playpal palette)
		{
			// Initialize
			this.palette = palette;
			
			// We have no destructor
			GC.SuppressFinalize(this);
		}

		#endregion

		#region ================== Methods
		
		// This validates the data as doom picture
		public bool Validate(Stream stream)
		{
			BinaryReader reader = new BinaryReader(stream);

			// Initialize
			int datalength = (int)stream.Length - (int)stream.Position;

			// Need at least 4 bytes
			if(datalength < 4) return false;

			// Read size and offset
			int width = reader.ReadInt16();
			int height = reader.ReadInt16();
			reader.ReadInt16();
			reader.ReadInt16();

			// Valid width and height?
			if(width < 1 || height < 1) return false;
			
			// Go for all columns
			for(int x = 0; x < width; x++)
			{
				// Get column address
				int columnaddr = reader.ReadInt32();

				// Check if address is outside valid range
				if((columnaddr < (8 + width * 4)) || (columnaddr >= datalength)) return false;
			}

			// Return success
			return true;
		}
		
		// This creates a Bitmap from the given data
		// Returns null on failure
		public PixelData ReadAsBitmap(Stream stream, out int offsetx, out int offsety)
		{
			int width, height;
			PixelColor[] pixeldata = ReadAsPixelData(stream, out width, out height, out offsetx, out offsety);
			if (pixeldata != null)
				return new PixelData(width, height, pixeldata);
			else
				return null;
		}
		
		// This creates pixel color data from the given data
		// Returns null on failure
		private PixelColor[] ReadAsPixelData(Stream stream, out int width, out int height, out int offsetx, out int offsety)
		{
			BinaryReader reader = new BinaryReader(stream);

			// Initialize
			width = 0;
			height = 0;
			offsetx = 0;
			offsety = 0;
			int dataoffset = (int)stream.Position;

			// Need at least 4 bytes
			if((stream.Length - stream.Position) < 4) return null;
			
			#if !DEBUG
			try
			{
			#endif
			
			// Read size and offset
			width = reader.ReadInt16();
			height = reader.ReadInt16();
			offsetx = reader.ReadInt16();
			offsety = reader.ReadInt16();
			
			// Valid width and height?
			if((width <= 0) || (height <= 0)) return null;
			
			// Read the column addresses
			int[] columns = new int[width];
			for(int x = 0; x < width; x++) columns[x] = reader.ReadInt32();
			
			// Allocate memory
			PixelColor[] pixeldata = new PixelColor[width * height];
			
			// Go for all columns
			for(int x = 0; x < width; x++)
			{
				// Seek to column start
				stream.Seek(dataoffset + columns[x], SeekOrigin.Begin);
				
				// Read first post start
				int y = reader.ReadByte();
				int read_y = y;
				
				// Continue while not end of column reached
				while(read_y < 255)
				{
					// Read number of pixels in post
					int count = reader.ReadByte();

					// Skip unused pixel
					stream.Seek(1, SeekOrigin.Current);

					// Draw post
					for(int yo = 0; yo < count; yo++)
					{
						// Read pixel color index
						int p = reader.ReadByte();

						//mxd. Sanity check required...
						int offset = (y + yo) * width + x;
						if(offset > pixeldata.Length - 1) return null;

						// Draw pixel
						pixeldata[offset] = palette[p];
					}

					// Skip unused pixel
					stream.Seek(1, SeekOrigin.Current);

					// Read next post start
					read_y = reader.ReadByte();
					if(read_y < y || (height > 256 && read_y == y)) y += read_y; else y = read_y; //mxd. Fix for tall patches higher than 508 pixels
				}
			}

			// Return pointer
			return pixeldata;
			
			#if !DEBUG
			}
			catch(Exception)
			{
				// Return nothing
				return null;
			}
			#endif
		}
		
		#endregion
	}
}
