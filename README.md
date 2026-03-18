# 📂 SmartFoldering

![Platform](https://img.shields.io/badge/Platform-Windows%20%7C%20macOS%20%7C%20Linux-lightgray)
![Framework](https://img.shields.io/badge/Framework-.NET%20Core%20%7C%20Avalonia%20UI-512BD4)
![Architecture](https://img.shields.io/badge/Architecture-MVVM-success)
![License](https://img.shields.io/badge/License-MIT-blue)

**SmartFoldering** is an enterprise-grade, automated file organization tool built with .NET Core and Avalonia UI. It silently monitors your specified directories in the background and instantly routes incoming files to their designated destination folders based on custom rules (extensions or keywords).

---

## ✨ Key Features

* **⚡ Real-Time Monitoring:** Utilizes `FileSystemWatcher` to detect and move files the exact millisecond they are created or renamed.
* **🧠 Smart Rule Engine:** Route files based on exact extensions (e.g., `.pdf`, `.jpg`) or specific keywords in the filename (e.g., `invoice`, `report`).
* **🛡️ Enterprise Reliability:** * **Thread-Safe & Lock Handling:** Intelligently waits for large files to finish downloading or copying before attempting to move them.
  * **Atomic Saves:** User settings are saved using temporary files and swapping, completely preventing data corruption during unexpected power losses.
  * **Single Instance (Mutex):** Prevents multiple instances of the app from running and causing race conditions.
* **👻 Silent Background Operation:** Runs seamlessly in the system tray. Includes a "Run at Windows Startup" feature via Registry integration.
* **📝 Professional Logging:** Integrated with **Serilog**. All events, warnings, and errors are safely logged in the `AppData` folder for easy debugging.
* **🎨 Modern UI/UX:** Features a distraction-free, premium dark-grayscale design built with Avalonia UI.

## 🚀 How It Works

1. **Add a Source Folder:** (e.g., your `Downloads` or `Desktop` folder).
2. **Add Target Folders:** (e.g., `Invoices`, `Photos`, `Installers`).
3. **Define Rules:** Tell the app to move `.pdf` files to `Documents`, or files containing the word "setup" to the `Installers` folder.
4. **Sit Back & Relax:** SmartFoldering handles the rest in the background!

## 📸 Screenshots

<p align="center">
  <img src="" width="600" title="ActiveRest Dashboard">
</p>
 

| Main Dashboard | Rule Configuration |
| :---: | :---: |
| ![Main](https://via.placeholder.com/400x300.png?text=Main+Window+Screenshot) | ![Rules](https://via.placeholder.com/400x300.png?text=Rules+Window+Screenshot) |

## 🛠️ Tech Stack & Architecture

* **Framework:** .NET Core
* **UI Framework:** [Avalonia UI](https://avaloniaui.net/) (Cross-platform desktop application framework)
* **Architecture:** Strict **MVVM** (Model-View-ViewModel) using `CommunityToolkit.Mvvm`
* **Logging:** `Serilog` (File Sinks)
* **Serialization:** `System.Text.Json` (Async & culture-independent)

---
---

# 📂 SmartFoldering (Türkçe)

**SmartFoldering**, .NET Core ve Avalonia UI kullanılarak geliştirilmiş, kurumsal (enterprise) seviyede otomatik bir dosya düzenleme aracıdır. Belirlediğiniz klasörleri arka planda sessizce izler ve gelen dosyaları özel kurallarınıza (uzantı veya anahtar kelime) göre anında hedef klasörlerine taşır.

---

## ✨ Öne Çıkan Özellikler

* ** Gerçek Zamanlı İzleme:** Dosyalar oluşturulduğu veya yeniden adlandırıldığı milisaniye içerisinde `FileSystemWatcher` ile tespit edilip taşınır.
* ** Akıllı Kural Motoru:** Dosyaları tam uzantılarına (örn. `.pdf`, `.jpg`) veya dosya adındaki kelimelere (örn. `fatura`, `rapor`) göre yönlendirin.
* ** Kurumsal Güvenilirlik (Enterprise Reliability):** * **Kilit Yönetimi (File Lock Handling):** Büyük dosyaların indirilmesinin veya kopyalanmasının bitmesini sabırla bekler, dosya kilidi açılmadan işlem yapmaz.
  * **Atomik Kayıt (Atomic Save):** Ayar dosyaları geçici dosyalar üzerinden yazılır. Elektrik kesintisi veya çökme durumunda veri kaybı/bozulması yaşanmaz.
  * **Tekil Çalışma (Mutex):** Uygulamanın yanlışlıkla birden fazla kez açılıp sistemin çökmesini engeller.
* ** Sessiz Arka Plan Çalışması:** Sistem tepsisinde (Tray) yorulmadan çalışır. "Windows Başlangıcında Çalıştır" (Kayıt Defteri entegrasyonu) özelliği içerir.
* ** Profesyonel Loglama:** **Serilog** entegrasyonu sayesinde tüm işlemler, uyarılar ve hatalar `AppData` klasöründe günlük olarak güvenle saklanır.
* ** Modern UI/UX:** Avalonia UI ile tasarlanmış, dikkat dağıtmayan, koyu gri tonlarda premium kurumsal arayüz.

## 🚀 Nasıl Çalışır?

1. **Kaynak Klasör Ekleyin:** (Örn: `İndirilenler` veya `Masaüstü` klasörünüz).
2. **Hedef Klasörler Ekleyin:** (Örn: `Faturalar`, `Fotoğraflar`, `Kurulum Dosyaları`).
3. **Kurallar Belirleyin:** Uygulamaya `.pdf` uzantılı dosyaları `Belgeler` klasörüne, içinde "setup" geçen dosyaları `Kurulum Dosyaları` klasörüne taşımasını söyleyin.
4. **Arkanıza Yaslanın:** SmartFoldering arka planda tüm işi sizin yerinize yapsın!

## 🛠️ Kullanılan Teknolojiler ve Mimari

* **Altyapı:** .NET Core
* **Arayüz (UI):** [Avalonia UI](https://avaloniaui.net/) (Çapraz platform masaüstü uygulama altyapısı)
* **Mimari:** `CommunityToolkit.Mvvm` kullanılarak tam bağımsız **MVVM** (Model-View-ViewModel)
* **Loglama:** `Serilog` (Günlük dosya kaydı)
* **Serileştirme:** `System.Text.Json` (Asenkron ve kültürel bağımsız - Culture Invariant)

---
**License / Lisans:** [MIT License](LICENSE)
