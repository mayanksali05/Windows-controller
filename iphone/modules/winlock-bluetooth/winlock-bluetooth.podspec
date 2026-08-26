Pod::Spec.new do |s|
  s.name           = "winlock-bluetooth"
  s.version        = "0.5.0"
  s.summary        = "WinLock Bluetooth module (CoreBluetooth proximity advertising)"
  s.description    = "Advertises the iPhone's per-device BLE service so the Windows laptop can estimate proximity."
  s.homepage       = "https://example.com/winlock"
  s.license        = "MIT"
  s.author         = { "WinLock" => "winlock@example.com" }
  s.source         = { :git => "https://example.com/winlock.git", :tag => "#{s.version}" }
  s.ios.deployment_target = "15.1"
  s.swift_version  = "5.9"
  s.source_files   = "ios/*.swift"
  s.dependency "ExpoModulesCore"
end