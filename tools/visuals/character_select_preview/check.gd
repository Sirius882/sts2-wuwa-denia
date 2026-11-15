extends Node

var checks = 0
var wraps = 0

func check(value: bool, message: String):
    if not value:
        push_error(message)
        get_tree().quit(1)
    checks += 1

func capture(viewport: SubViewport, name: String):
    await RenderingServer.frame_post_draw
    check(viewport.get_texture().get_image().save_png("res://captures/" + name + ".png") == OK, "Capture " + name)

func _ready():
    DirAccess.make_dir_recursive_absolute("res://captures")
    var viewport = SubViewport.new()
    viewport.size = Vector2i(960, 540)
    viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
    add_child(viewport)
    var parent = Control.new()
    parent.size = Vector2(viewport.size) + Vector2(640, 120)
    parent.position = Vector2(-388, -80)
    parent.pivot_offset = Vector2(1280, 600)
    parent.scale = Vector2(1.1, 1.1)
    viewport.add_child(parent)
    var packed = load("res://scenes/screens/char_select/denia_bg.tscn")
    var background = packed.instantiate()
    parent.add_child(background)
    var video: VideoStreamPlayer = background.get_node("Video")
    var poster: TextureRect = background.get_node("Poster")
    check(video.loop and video.speed_scale == 1 and video.expand, "Full original speed with native looping")
    check(video.volume == 0, "Silent source preserves menu audio")
    check(poster.visible and not video.visible, "New source poster covers initial video decoding")
    for node in [background, video, poster]:
        check(node.mouse_filter == Control.MOUSE_FILTER_IGNORE, "Background never captures selection input")
    var previous = 0.0
    for frame in range(850):
        await RenderingServer.frame_post_draw
        var position = video.stream_position
        if position + 0.1 < previous:
            wraps += 1
        previous = position
        if frame == 180:
            parent.position += Vector2(50, -40)
        check(poster.visible or video.visible, "Background always has a visible layer")
        if frame in [30, 240, 430, 820]:
            await capture(viewport, "loop_%04d" % frame)
    check(wraps >= 2 and video.is_playing(), "Video crosses its end and loops at least twice")
    check(video.visible and not poster.visible, "Decoded video replaces poster")
    check(video.get_video_texture().get_size() == Vector2(3840, 2160), "Native decode retains 4K resolution")
    parent.hide()
    check(video.paused, "Hidden character-select container pauses decoding")
    var stopped = video.stream_position
    for frame in range(30):
        await RenderingServer.frame_post_draw
    check(abs(video.stream_position - stopped) < 0.001, "Hidden background clock remains frozen")
    parent.show()
    check(not video.paused, "Reappearing character-select container resumes playback")
    for frame in range(10):
        await RenderingServer.frame_post_draw
    check(abs(video.stream_position - stopped) > 0.05, "Playback advances after reappearing")
    for size in [Vector2i(1280, 800), Vector2i(1280, 540), Vector2i(960, 720)]:
        viewport.size = size
        parent.size = Vector2(size) + Vector2(640, 120)
        await RenderingServer.frame_post_draw
        check(background.size.is_equal_approx(size), "Background follows viewport dimensions")
        check(background.get_global_transform().get_scale().is_equal_approx(Vector2.ONE), "Native background cancels inherited parallax scaling")
        check(is_equal_approx(video.size.x / video.size.y, 16.0 / 9.0), "Video retains authored aspect ratio")
        check(video.position.x <= 0.01 and video.position.y <= 0.01
            and video.position.x + video.size.x >= size.x - 0.01
            and video.position.y + video.size.y >= size.y - 0.01, "Centered video covers screen without new bars")
        await capture(viewport, "layout_%dx%d" % [size.x, size.y])
    parent.remove_child(background)
    check(not video.is_playing(), "Leaving selected character stops video")
    background.free()
    background = packed.instantiate()
    parent.add_child(background)
    video = background.get_node("Video")
    check(video.is_playing() and video.stream_position < 0.1, "Selecting Denia again starts a fresh video")
    background.free()
    var report = {"checks": checks, "native_loop_wraps": wraps, "render": DisplayServer.get_name()}
    FileAccess.open("res://captures/checks.json", FileAccess.WRITE).store_string(JSON.stringify(report, "  "))
    print("PASS: ", JSON.stringify(report))
    get_tree().quit()
