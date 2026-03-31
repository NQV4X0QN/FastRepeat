use gtk4 as gtk;
use gtk4::prelude::*;
use gtk4::glib;
use libadwaita as adw;
use libadwaita::prelude::*;

use std::cell::RefCell;
use std::rc::Rc;

use crate::config::{AppSettings, KeyBinding};
use crate::input::{is_mouse_button, key_name};

/// Launch the GTK4 GUI application
pub fn run_gui() {
    let app = adw::Application::builder()
        .application_id("io.github.nqv4x0qn.fastrepeat")
        .build();

    app.connect_activate(build_ui);
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

    // Enable/Disable switch
    {
        let s = settings.clone();
        enable_switch.connect_state_set(move |_, state| {
            s.borrow_mut().is_enabled = state;
            let _ = s.borrow().save();
            glib::Propagation::Proceed
        });
    }

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

fn show_add_dialog(
    window: &adw::ApplicationWindow,
    settings: &Rc<RefCell<AppSettings>>,
    list: &gtk::ListBox,
) {
    let dialog = gtk::MessageDialog::builder()
        .transient_for(window)
        .modal(true)
        .message_type(gtk::MessageType::Info)
        .buttons(gtk::ButtonsType::Ok)
        .text("Add Key Binding")
        .secondary_text("Key capture requires the CLI.\n\nRun this command in a terminal:\n\n  fastrepeat add\n\nThen restart the GUI to see the new binding.")
        .build();

    let s = settings.clone();
    let l = list.clone();
    dialog.connect_response(move |dlg, _| {
        dlg.close();
        let reloaded = AppSettings::load();
        *s.borrow_mut() = reloaded;
        populate_bindings_list(&l, &s.borrow());
    });
    dialog.present();
}

fn show_set_output_dialog(
    window: &adw::ApplicationWindow,
    settings: &Rc<RefCell<AppSettings>>,
    _list: &gtk::ListBox,
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

    let dialog = gtk::MessageDialog::builder()
        .transient_for(window)
        .modal(true)
        .message_type(gtk::MessageType::Info)
        .buttons(gtk::ButtonsType::Ok)
        .text("Set Output Key")
        .secondary_text(&format!(
            "Trigger: {}\n\nTo change the output key, run:\n\n  fastrepeat remove {}\n  fastrepeat add\n\nThen restart the GUI.",
            trigger_name, index
        ))
        .build();
    dialog.connect_response(|dlg, _| dlg.close());
    dialog.present();
}
