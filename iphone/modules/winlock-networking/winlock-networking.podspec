Pod::Spec.new do |s|
  s.name           = "winlock-networking"
  s.version        = "0.5.0"
  s.summary        = "WinLock networking module (pinned HTTPS + Bonjour discovery)"
  s.description    = "TLS-pinned requests and Bonjour/mDNS discovery for the WinLock iOS client."
  s.homepage       = "https://example.com/winlock"
  s.license        = "MIT"
  s.author         = { "WinLock" => "winlock@example.com" }
  s.source         = { :git => "https://example.com/winlock.git", :tag => "#{s.version}" }
  s.ios.deployment_target = "15.1"
  s.swift_version  = "5.9"
  s.source_files   = "ios/*.swift"
  s.dependency "ExpoModulesCore"
end