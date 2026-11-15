extends SceneTree

# Run through the installed game's normal mod initializer, not a standalone preview assembly.
# Do not open a run or change character selections/saves.
func _initialize():
    call_deferred("probe")

func require(value: bool, message: String) -> bool:
    if not value:
        push_error("DENIA_BG_GAME: " + message)
        quit(1)
    return value

func probe():
    var main_path = ProjectSettings.get_setting("application/run/main_scene")
    var main = load(main_path).instantiate()
    root.add_child(main)
    current_scene = main
    await create_timer(12.0).timeout
    var packed = load("res://scenes/screens/char_select/denia_bg.tscn")
    if not require(packed != null, "Installed background scene is mounted"):
        return
    var background = packed.instantiate()
    root.add_child(background)
    await process_frame
    var script = background.get_script()
    if not require(script != null and script.can_instantiate(), "Denia C# script is registered by mod initialization"):
        return
    var video = background.get_node("Video") as VideoStreamPlayer
    var poster = background.get_node("Poster") as TextureRect
    await create_timer(0.5).timeout
    if not require(video.is_playing() and video.stream_position > 0 and video.visible and not poster.visible,
            "Installed video decodes, advances and replaces the poster"):
        return
    if not require(background.size.is_equal_approx(background.get_viewport_rect().size), "Driver sizes background to viewport"):
        return
    background.hide()
    var stopped = video.stream_position
    await create_timer(0.2).timeout
    if not require(video.paused and abs(video.stream_position - stopped) < 0.001, "Hidden background pauses decoding"):
        return
    background.show()
    await create_timer(0.2).timeout
    if not require(not video.paused and video.stream_position > stopped, "Visible background resumes decoding"):
        return
    print("DENIA_BG_GAME PASS: registered scene; native video visible and advancing; poster/layout/hide/resume")
    background.free()
    quit()
