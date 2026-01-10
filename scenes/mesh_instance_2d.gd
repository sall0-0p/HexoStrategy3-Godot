extends MeshInstance2D

func _ready() -> void:
	# Define the vertices of the shape (a triangle in this case)
	var vertices = PackedVector2Array([
		Vector2(0, -50),
		Vector2(50, 50),
		Vector2(-50, 50)
	])

	# Define how the texture maps to these vertices (0 to 1 range)
	var uvs = PackedVector2Array([
		Vector2(0.5, 0),
		Vector2(1, 1),
		Vector2(0, 1)
	])

	# Define the color for each vertex (optional, white modulates nothing)
	var colors = PackedColorArray([
		Color(1, 1, 1),
		Color(1, 1, 1),
		Color(1, 1, 1)
	])

	# Initialize the ArrayMesh
	var array_mesh = ArrayMesh.new()
	var arrays = []
	arrays.resize(Mesh.ARRAY_MAX)

	arrays[Mesh.ARRAY_VERTEX] = vertices
	arrays[Mesh.ARRAY_TEX_UV] = uvs
	arrays[Mesh.ARRAY_COLOR] = colors

	# Create the mesh surface
	array_mesh.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES, arrays)

	# Assign the created mesh to this node
	mesh = array_mesh

	# Load a texture to display on the mesh
	texture = load("res://media/map/terrain/atlas0.dds")
