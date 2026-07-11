# frozen_string_literal: true

Gem::Specification.new do |spec|
  spec.name = "tdd-guard-minitest"
  spec.version = "0.2.0"
  spec.authors = ["Hiro-Chiba"]
  spec.summary = "Minitest reporter for TDD Guard - enforces Test-Driven Development principles"
  spec.description = "Minitest reporter that captures test results for TDD Guard validation."
  spec.homepage = "https://github.com/nizos/tdd-guard"
  spec.license = "MIT"
  spec.required_ruby_version = ">= 3.3.0"

  spec.files = Dir["lib/**/*.rb"]
  spec.require_paths = ["lib"]

  spec.add_dependency "minitest", "~> 5.0"

  spec.add_development_dependency "rspec", "~> 3.0"
  spec.add_development_dependency "climate_control", "~> 1.0"
end
