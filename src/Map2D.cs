using Godot;
using System;
using System.Collections.Generic;

public partial class Map2D : Node2D
{
	private Sprite2D _terrainSprite;
	
	private readonly Dictionary<string, MapProvince> _provinces = new();
	private readonly Dictionary<int, string> _colorToProvinceId = new();
	private readonly HashSet<Color> _colors = new();

	private readonly string _terrainTextureAtlas = "res://media/map/terrain/atlas1.dds";
	private readonly Dictionary<Color, int[]> _colorsToTerrainTextures = new()
	{
		{ Color.FromHtml("567c1bff"), [0, 0] },
	};
	
	private record MapProvince(
		string Id,
		Vector2[] Pixels,
		bool IsWater,
		bool IsCoastal,
		string Terrain,
		int Continent,
		Color ColorKey
	);
	
	public override void _Ready()
	{
		GD.Print("Hello World!");
		_terrainSprite = GetNode<Sprite2D>("TerrainSprite");
		_drawTerrainMap();
	}
	
	public override void _Process(double delta)
	{
	}

	private void _drawTerrainMap()
	{
		var provincesBmp = _loadImage("res://media/map/hoi4/provinces.bmp");
		var heightmapBmp = _loadImage("res://media/map/hoi4/heightmap.bmp");
		var terrainBmp = _loadImage("res://media/map/hoi4/terrain.bmp");
		var terrainTextureAtlas = _loadImageAsTexture(_terrainTextureAtlas);
		
		_buildProvinceDict();
		_scanProvincePixels(provincesBmp);
		
		var terrainImage = Image.CreateEmpty(provincesBmp.GetWidth(), provincesBmp.GetHeight(), false, Image.Format.Rgba8);

		foreach (var p in _provinces.Values)
		{
			if (p.IsWater || p.Pixels.Length == 0) continue;
			
			foreach (var pixel in p.Pixels)
			{
				var x = (int)pixel.X;
				var y = (int)pixel.Y;
				
				Color finalColor;
				var terrainColor = terrainBmp.GetPixel(x, y);
				if (_colorsToTerrainTextures.ContainsKey(terrainColor))
				{
					var atlasOffsetX = 256 * _colorsToTerrainTextures[terrainColor][0];
					var atlasOffsetY = 256 * _colorsToTerrainTextures[terrainColor][1];
					var finalTextureX = atlasOffsetX + (x % 256);
					var finalTextureY = atlasOffsetY + (y % 256);
					finalColor = terrainTextureAtlas.GetPixel(finalTextureX, finalTextureY);
				}
				else
				{
					_colors.Add(terrainColor);
					finalColor = new Color(0, 0, 0);
				}

				terrainImage.SetPixel(x, y, finalColor);
			}
		}

		_terrainSprite.Texture = ImageTexture.CreateFromImage(terrainImage);

		foreach (var color in _colors)
		{
			GD.Print(color.R + ", ", color.G + ", ", color.B + ", ", color.ToHtml());
		}
	}

	private Image _loadImage(string path)
	{
		var image = new Image();
		var err = image.Load(path);
		if (err != Error.Ok) {
			GD.PushError("Failed to load" + path + ": " + err);
			return new Image();
		}
		GD.Print("Loaded {0}");
		return image;
	}

	private Image _loadImageAsTexture(string path)
	{
		var texture = GD.Load<Texture2D>(path);
		if (texture == null)
		{
			GD.PushError($"Failed to load texture resource: {path}");
			return new Image();
		}
		
		var image = texture.GetImage();
		if (image.IsCompressed())
		{
			image.Decompress();
		}

		return image;
	}
	
	private void _buildProvinceDict()
	{
		var file = FileAccess.Open("res://media/map/hoi4/definition.csv", FileAccess.ModeFlags.Read);
		if (file == null)
		{
			GD.PushError("Failed to open definition.csv");
			return;
		}

		while (!file.EofReached())
		{
			var line = file.GetLine().Trim();
			if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
				continue;

			var parts = line.Split(';');
			if (parts.Length < 8)
				continue;

			var id = parts[0];

			if (!int.TryParse(parts[1], out var r)) continue;
			if (!int.TryParse(parts[2], out var g)) continue;
			if (!int.TryParse(parts[3], out var b)) continue;

			var type = parts[4];
			var isWater = type.Equals("sea", StringComparison.OrdinalIgnoreCase) ||
			              type.Equals("lake", StringComparison.OrdinalIgnoreCase);
			var isCoastal = parts[5].Equals("true", StringComparison.OrdinalIgnoreCase);
			var terrain = parts[6];
			var continent = int.TryParse(parts[7], out var c) ? c : -1;

			var colorKey = new Color(r / 255.0f, g / 255.0f, b / 255.0f, 1f);

			_provinces[id] = new MapProvince(
				id,
				Array.Empty<Vector2>(), // will be filled by ScanProvincePixels
				isWater,
				isCoastal,
				terrain,
				continent,
				colorKey
			);

			_colorToProvinceId[_packRgb(r, g, b)] = id;
		}

		GD.Print($"Loaded {_provinces.Count} provinces from definition.csv");
	}

	private static int _packRgb(int r, int g, int b) => (r << 16) | (g << 8) | b;
	private void _scanProvincePixels(Image provinceMap)
	{
		if (provinceMap == null)
		{
			GD.PushError("provinceMap not loaded");
			return;
		}

		var w = provinceMap.GetWidth();
		var h = provinceMap.GetHeight();

		// temp buckets so we don't reallocate arrays per pixel
		var buckets = new Dictionary<string, List<Vector2>>(_provinces.Count);

		for (var y = 0; y < h; y++)
		{
			for (var x = 0; x < w; x++)
			{
				var c = provinceMap.GetPixel(x, y);

				// quantize float color to 0..255 like CSV
				var r = (int)MathF.Round(c.R * 255f);
				var g = (int)MathF.Round(c.G * 255f);
				var b = (int)MathF.Round(c.B * 255f);

				var key = _packRgb(r, g, b);
				if (!_colorToProvinceId.TryGetValue(key, out var id))
					continue;

				if (!buckets.TryGetValue(id, out var list))
				{
					list = new List<Vector2>(256);
					buckets[id] = list;
				}

				list.Add(new Vector2(x, y));
			}
		}

		foreach (var kv in buckets)
		{
			if (!_provinces.TryGetValue(kv.Key, out var p))
				continue;

			_provinces[kv.Key] = p with { Pixels = kv.Value.ToArray() };
		}

		GD.Print("Finished scanning province pixels.");
	}
}
