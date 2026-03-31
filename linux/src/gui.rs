use gtk4 as gtk;
use gtk4::prelude::*;
use gtk4::glib;
use libadwaita as adw;
use libadwaita::prelude::*;

use std::cell::RefCell;
use std::rc::Rc;

use crate::config::{AppSettings, KeyBinding};
use crate::input::{is_mouse_button, key_name, capture_key_async, CapturedKey};
use crate::tray::{self, TrayAction};

/// Launch the GTK4 GUI application
pub fn run_gui() {
    let app = adw::Application::builder()
        .application_id("io.github.nqv4x0qn.fastrepeat")
        .build();

    app.connect_activate(build_ui);

    // Hold the tray handle to keep it alive for the app lifetime
    app.connect_startup(|_| {
        log::info!("GTK application started");
    });

    app.run_with_args::<String>(&[]);
}

fn build_ui(app: &adw::Application) {
    // Use AdwStyleManager for theme preference instead of the deprecated
    // GtkSettings:gtk-application-prefer-dark-theme (suppresses the warning)
    let style_manager = adw::StyleManager::default();
    style_manager.set_color_scheme(adw::ColorScheme::Default);

    let settings = Rc::new(RefCell::new(AppSettings::load()));

    // Main window
    let window = adw::ApplicationWindow::builder()
        .application(app)
        .title("Fast Repeat")
        .default_width(680)
        .default_height(580)
        .build();

    // Main content box
    let content = gtk::Box::new(gtk::Orientation::Vertical, 0);

    // Header bar
    let header = adw::HeaderBar::new();
    let title = adw::WindowTitle::new("Fast Repeat", "Key & mouse button repeater");
    header.set_title_widget(Some(&title));

    // Enable/Disable toggle in header
    let enable_switch = gtk::Switch::new();
    enable_switch.set_active(settings.borrow().is_enabled);
    enable_switch.set_valign(gtk::Align::Center);
    header.pack_end(&enable_switch);

    content.append(&header);

    // Scrollable body
    let scroll = gtk::ScrolledWindow::builder()
        .vexpand(true)
        .build();

    let body = gtk::Box::new(gtk::Orientation::Vertical, 12);
    body.set_margin_start(16);
    body.set_margin_end(16);
    body.set_margin_top(12);
    body.set_margin_bottom(12);

    // ── Key Bindings Section ──────────────────────────────────────
    let bindings_group = adw::PreferencesGroup::new();
    bindings_group.set_title("Key Bindings");
    bindings_group.set_description(Some("Keys and mouse buttons that repeat when held"));

    // Bindings list box
    let bindings_list = gtk::ListBox::new();
    bindings_list.set_selection_mode(gtk::SelectionMode::Single);
    bindings_list.add_css_class("boxed-list");

    // Populate bindings
    let settings_ref = settings.clone();
    populate_bindings_list(&bindings_list, &settings_ref.borrow());

    bindings_group.add(&bindings_list);

    // Action buttons row
    let btn_box = gtk::Box::new(gtk::Orientation::Horizontal, 8);
    btn_box.set_margin_top(8);

    let add_btn = gtk::Button::with_label("Add Key / Button");
    add_btn.add_css_class("suggested-action");
    let remove_btn = gtk::Button::with_label("Remove");
    remove_btn.set_sensitive(false);
    let mode_btn = gtk::Button::with_label("Toggle Mode");
    mode_btn.set_sensitive(false);
    let output_btn = gtk::Button::with_label("Set Output");
    output_btn.set_sensitive(false);

    btn_box.append(&add_btn);
    btn_box.append(&remove_btn);
    btn_box.append(&mode_btn);
    btn_box.append(&output_btn);

    bindings_group.add(&btn_box);
    body.append(&bindings_group);

    // ── Repeat Speed Section ──────────────────────────────────────
    let speed_group = adw::PreferencesGroup::new();
    speed_group.set_title("Repeat Speed");

    // Speed slider row
    let speed_row = adw::ActionRow::builder()
        .title("Interval")
        .subtitle("Milliseconds between repeats")
        .build();

    let speed_adj = gtk::Adjustment::new(
        settings.borrow().repeat_interval_ms as f64,
        25.0, 600.0, 5.0, 25.0, 0.0,
    );
    let speed_scale = gtk::Scale::new(gtk::Orientation::Horizontal, Some(&speed_adj));
    speed_scale.set_width_request(200);
    speed_scale.set_valign(gtk::Align::Center);
    speed_scale.set_draw_value(true);
    speed_scale.set_value_pos(gtk::PositionType::Left);

    let speed_spin = gtk::SpinButton::new(Some(&speed_adj), 1.0, 0);
    speed_spin.set_valign(gtk::Align::Center);
    speed_spin.set_width_chars(4);

    let ms_label = gtk::Label::new(Some("ms"));
    ms_label.set_valign(gtk::Align::Center);
    ms_label.add_css_class("dim-label");

    speed_row.add_suffix(&speed_scale);
    speed_row.add_suffix(&speed_spin);
    speed_row.add_suffix(&ms_label);

    speed_group.add(&speed_row);

    // Lock speed row
    let lock_row = adw::ActionRow::builder()
        .title("Lock Speed")
        .subtitle("Prevent accidental speed changes")
        .build();
    let lock_switch = gtk::Switch::new();
    lock_switch.set_active(settings.borrow().is_speed_locked);
    lock_switch.set_valign(gtk::Align::Center);
    lock_row.add_suffix(&lock_switch);
    lock_row.set_activatable_widget(Some(&lock_switch));

    speed_group.add(&lock_row);
    body.append(&speed_group);

    // ── Version footer ────────────────────────────────────────────
    let version_label = gtk::Label::new(Some(&format!("v{}", env!("CARGO_PKG_VERSION"))));
    version_label.add_css_class("dim-label");
    version_label.add_css_class("caption");
    version_label.set_margin_top(8);
    body.append(&version_label);

    scroll.set_child(Some(&body));
    content.append(&scroll);
    window.set_content(Some(&content));

    // ── Connect signals ───────────────────────────────────────────

    // Enable/Disable switch — synced with tray state below in the tray section

    // Speed slider
    {
        let s = settings.clone();
        let lock_sw = lock_switch.clone();
        speed_adj.connect_value_changed(move |adj| {
            if !lock_sw.is_active() {
                s.borrow_mut().repeat_interval_ms = adj.value() as u64;
                let _ = s.borrow().save();
            }
        });
    }

    // Lock speed
    {
        let s = settings.clone();
        let scale = speed_scale.clone();
        let spin = speed_spin.clone();
        lock_switch.connect_state_set(move |_, locked| {
            s.borrow_mut().is_speed_locked = locked;
            let _ = s.borrow().save();
            scale.set_sensitive(!locked);
            spin.set_sensitive(!locked);
            glib::Propagation::Proceed
        });
        // Initial state
        speed_scale.set_sensitive(!settings.borrow().is_speed_locked);
        speed_spin.set_sensitive(!settings.borrow().is_speed_locked);
    }

    // Selection tracking for buttons
    {
        let rm = remove_btn.clone();
        let md = mode_btn.clone();
        let out = output_btn.clone();
        bindings_list.connect_row_selected(move |_, row| {
            let has = row.is_some();
            rm.set_sensitive(has);
            md.set_sensitive(has);
            out.set_sensitive(has);
        });
    }

    // Add button
    {
        let s = settings.clone();
        let list = bindings_list.clone();
        let win = window.clone();
        add_btn.connect_clicked(move |_| {
            show_add_dialog(&win, &s, &list);
        });
    }

    // Remove button
    {
        let s = settings.clone();
        let list = bindings_list.clone();
        remove_btn.connect_clicked(move |_| {
            if let Some(row) = list.selected_row() {
                let idx = row.index() as usize;
                s.borrow_mut().bindings.remove(idx);
                let _ = s.borrow().save();
                populate_bindings_list(&list, &s.borrow());
            }
        });
    }

    // Toggle Mode button
    {
        let s = settings.clone();
        let list = bindings_list.clone();
        mode_btn.connect_clicked(move |_| {
            if let Some(row) = list.selected_row() {
                let idx = row.index() as usize;
                let mut settings = s.borrow_mut();
                if idx < settings.bindings.len() {
                    let b = &mut settings.bindings[idx];
                    b.mode = if b.mode == "repeat" {
                        "single_press".to_string()
                    } else {
                        "repeat".to_string()
                    };
                    let _ = settings.save();
                    drop(settings);
                    populate_bindings_list(&list, &s.borrow());
                }
            }
        });
    }

    // Set Output button
    {
        let s = settings.clone();
        let list = bindings_list.clone();
        let win = window.clone();
        output_btn.connect_clicked(move |_| {
            if let Some(row) = list.selected_row() {
                let idx = row.index() as usize;
                show_set_output_dialog(&win, &s, &list, idx);
            }
        });
    }

    // ── System tray ───────────────────────────────────────────────
    let initial_enabled = settings.borrow().is_enabled;
    let (tray_rx, tray_state, _tray_handle) = tray::start_tray(initial_enabled);

    // When window close is requested, hide instead of destroy (tray keeps running)
    window.connect_close_request(move |win| {
        win.set_visible(false);
        glib::Propagation::Stop
    });

    // Poll tray actions from the background thread
    {
        let win = window.clone();
        let s = settings.clone();
        let es = enable_switch.clone();
        let tray_st = tray_state.clone();
        let app_for_quit = app.clone();

        glib::timeout_add_local(std::time::Duration::from_millis(100), move || {
            while let Ok(action) = tray_rx.try_recv() {
                match action {
                    TrayAction::ShowWindow => {
                        win.set_visible(true);
                        win.present();
                    }
                    TrayAction::ToggleEnabled => {
                        let current = s.borrow().is_enabled;
                        let new_state = !current;
                        s.borrow_mut().is_enabled = new_state;
                        let _ = s.borrow().save();
                        es.set_active(new_state);
                        tray_st.is_enabled.store(new_state, std::sync::atomic::Ordering::Relaxed);
                    }
                    TrayAction::ToggleAutostart => {
                        tray::do_toggle_autostart(&tray_st);
                    }
                    TrayAction::Quit => {
                        app_for_quit.quit();
                        return glib::ControlFlow::Break;
                    }
                }
            }
            glib::ControlFlow::Continue
        });
    }

    // Sync tray state when enable switch changes in GUI
    {
        let ts = tray_state.clone();
        let s2 = settings.clone();
        enable_switch.connect_state_set(move |_, state| {
            ts.is_enabled.store(state, std::sync::atomic::Ordering::Relaxed);
            s2.borrow_mut().is_enabled = state;
            let _ = s2.borrow().save();
            glib::Propagation::Proceed
        });
    }

    window.present();
}

fn populate_bindings_list(list: &gtk::ListBox, settings: &AppSettings) {
    // Remove all existing rows
    while let Some(child) = list.first_child() {
        list.remove(&child);
    }

    if settings.bindings.is_empty() {
        let row = adw::ActionRow::builder()
            .title("No bindings configured")
            .subtitle("Click \"Add Key / Button\" to get started")
            .build();
        row.add_css_class("dim-label");
        list.append(&row);
        return;
    }

    for b in &settings.bindings {
        let trigger = key_name(b.trigger_code);
        let output = if b.output_code.is_some() {
            key_name(b.actual_output_code())
        } else {
            trigger.clone()
        };
        let mode_str = if b.mode == "single_press" { "Single Press" } else { "Repeat" };

        let row = adw::ActionRow::builder()
            .title(&format!("{} → {}", trigger, output))
            .subtitle(mode_str)
            .build();

        // Mode badge
        let badge = gtk::Label::new(Some(mode_str));
        badge.add_css_class("dim-label");
        badge.add_css_class("caption");
        badge.set_valign(gtk::Align::Center);
        row.add_suffix(&badge);

        list.append(&row);
    }
}

/// Show the native evdev capture dialog for adding a new key binding.
/// Two-step flow: capture trigger, then capture output, then mode selection.
fn show_add_dialog(
    window: &adw::ApplicationWindow,
    settings: &Rc<RefCell<AppSettings>>,
    list: &gtk::ListBox,
) {
    let s = settings.clone();
    let l = list.clone();
    let win = window.clone();

    // Step 1: Capture trigger
    show_capture_dialog(
        &win.clone(),
        "Step 1 of 2 — Capture Trigger",
        "Press the key or mouse button you want to use as the <b>trigger</b> (the key you hold down).",
        move |trigger| {
            let s2 = s.clone();
            let l2 = l.clone();
            let win2 = win.clone();
            let trigger = trigger.clone();

            // Step 2: Capture output
            show_capture_dialog(
                &win2.clone(),
                "Step 2 of 2 — Capture Output",
                &format!(
                    "Trigger: <b>{}</b>\n\nNow press the key to <b>repeat</b> as output, or press the same key for self-repeat.",
                    trigger.name
                ),
                move |output| {
                    let same_key = output.code == trigger.code;

                    // Step 3: Mode selection dialog
                    show_mode_dialog(&win2, &trigger, output, same_key, &s2, &l2);
                },
            );
        },
    );
}

/// Show the native evdev capture dialog for changing the output of an existing binding.
fn show_set_output_dialog(
    window: &adw::ApplicationWindow,
    settings: &Rc<RefCell<AppSettings>>,
    list: &gtk::ListBox,
    index: usize,
) {
    let trigger_name = {
        let s = settings.borrow();
        if index < s.bindings.len() {
            key_name(s.bindings[index].trigger_code)
        } else {
            return;
        }
    };

    let s = settings.clone();
    let l = list.clone();

    show_capture_dialog(
        window,
        "Set Output Key",
        &format!(
            "Trigger: <b>{}</b>\n\nPress the new key to <b>repeat</b> as output, or press the trigger key for self-repeat.",
            trigger_name
        ),
        move |output| {
            let mut settings = s.borrow_mut();
            if index < settings.bindings.len() {
                let b = &mut settings.bindings[index];
                let same_key = output.code == b.trigger_code;

                b.output_code = if same_key { None } else { Some(output.code) };
                b.output_is_mouse = if same_key { None } else { Some(output.is_mouse) };
                b.display_name = if same_key {
                    format!("{} → self", key_name(b.trigger_code))
                } else {
                    format!("{} → {}", key_name(b.trigger_code), output.name)
                };

                let _ = settings.save();
                drop(settings);
                populate_bindings_list(&l, &s.borrow());
            }
        },
    );
}

/// Core capture dialog — shows an Adwaita dialog that reads evdev input in a background thread.
/// Calls `on_captured` with the result when a key is pressed.
fn show_capture_dialog<F: Fn(&CapturedKey) + 'static>(
    window: &adw::ApplicationWindow,
    title: &str,
    markup: &str,
    on_captured: F,
) {
    let dialog = gtk::MessageDialog::builder()
        .transient_for(window)
        .modal(true)
        .message_type(gtk::MessageType::Question)
        .buttons(gtk::ButtonsType::Cancel)
        .text(title)
        .use_markup(true)
        .secondary_use_markup(true)
        .secondary_text(markup)
        .build();

    // Add a status label below the message
    let status_label = gtk::Label::new(Some("Listening for input…"));
    status_label.add_css_class("dim-label");
    status_label.set_margin_top(8);
    dialog.content_area().append(&status_label);

    // Start background evdev capture
    let (rx, cancel_flag) = capture_key_async();

    // Poll for capture result using a GLib timeout (runs on GTK main thread)
    let dialog_clone = dialog.clone();
    let status_label_clone = status_label.clone();

    glib::timeout_add_local(std::time::Duration::from_millis(50), move || {
        match rx.try_recv() {
            Ok(captured) => {
                // Key captured — close dialog and invoke callback
                dialog_clone.close();
                on_captured(&captured);
                glib::ControlFlow::Break
            }
            Err(std::sync::mpsc::TryRecvError::Empty) => {
                // Still waiting
                glib::ControlFlow::Continue
            }
            Err(std::sync::mpsc::TryRecvError::Disconnected) => {
                // Background thread ended without result (no devices / permission error)
                status_label_clone.set_text("✗ Cannot read input devices. Check 'input' group membership.");
                status_label_clone.remove_css_class("dim-label");
                status_label_clone.add_css_class("error");
                glib::ControlFlow::Break
            }
        }
    });

    // Cancel the background capture if user closes/cancels the dialog
    dialog.connect_response(move |dlg, _| {
        cancel_flag.store(true, std::sync::atomic::Ordering::Relaxed);
        dlg.close();
    });

    dialog.present();
}

/// Show mode selection dialog after both keys are captured, then save the binding.
fn show_mode_dialog(
    window: &adw::ApplicationWindow,
    trigger: &CapturedKey,
    output: &CapturedKey,
    same_key: bool,
    settings: &Rc<RefCell<AppSettings>>,
    list: &gtk::ListBox,
) {
    let summary = if same_key {
        format!("{} → self-repeat", trigger.name)
    } else {
        format!("{} → {}", trigger.name, output.name)
    };

    let dialog = gtk::MessageDialog::builder()
        .transient_for(window)
        .modal(true)
        .message_type(gtk::MessageType::Question)
        .text("Choose Repeat Mode")
        .secondary_text(&format!("{}\n\nHow should this binding behave?", summary))
        .build();

    dialog.add_button("Cancel", gtk::ResponseType::Cancel);
    dialog.add_button("Repeat While Held", gtk::ResponseType::Accept);
    dialog.add_button("Single Press", gtk::ResponseType::Other(1));

    // Style the primary action
    if let Some(btn) = dialog.widget_for_response(gtk::ResponseType::Accept) {
        btn.add_css_class("suggested-action");
    }

    let s = settings.clone();
    let l = list.clone();
    let trigger = trigger.clone();
    let output = output.clone();

    dialog.connect_response(move |dlg, response| {
        dlg.close();

        let mode = match response {
            gtk::ResponseType::Accept => "repeat",
            gtk::ResponseType::Other(1) => "single_press",
            _ => return, // cancelled
        };

        // Check for duplicate trigger
        {
            let settings = s.borrow();
            if settings.bindings.iter().any(|b| {
                b.trigger_code == trigger.code && b.trigger_is_mouse == trigger.is_mouse
            }) {
                // TODO: could show an error dialog here; for now just skip
                return;
            }
        }

        let binding = KeyBinding {
            trigger_code: trigger.code,
            trigger_is_mouse: trigger.is_mouse,
            output_code: if same_key { None } else { Some(output.code) },
            output_is_mouse: if same_key { None } else { Some(output.is_mouse) },
            mode: mode.to_string(),
            display_name: if same_key {
                format!("{} → self", trigger.name)
            } else {
                format!("{} → {}", trigger.name, output.name)
            },
        };

        s.borrow_mut().bindings.push(binding);
        let _ = s.borrow().save();
        populate_bindings_list(&l, &s.borrow());
    });

    dialog.present();
}
