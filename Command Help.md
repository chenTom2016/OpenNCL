# OpenNCL Command Help

![Python](https://img.shields.io/badge/Python-3.x-green.svg) ![License](https://img.shields.io/badge/License-MIT-yellow.svg)

**OpenNCL (Open New Command Line OS)** is a multi-functional command-line application developed by ChenTom2016, 

designed to provide a class-operating system experience integrating Python and Ruby environments. 

The latest version **3.1415926** introduces several functional enhancements and optimizations, 

notably the integration of **ClawBot (openclaw.ai)** AI support, which significantly expands its intelligent interaction capabilities.

## Table of Contents

- [Features](#features)
  - [1. Core System Functions](#1-core-system-functions)
  - [2. System Shortcuts](#2-system-shortcuts)
  - [3. Module Management](#3-module-management)
  - [4. Advanced Tools](#4-advanced-tools)
    - [4.1 Enhanced Calculator](#41-enhanced-calculator)
    - [4.2 Screenshot Tool](#42-screenshot-tool)
    - [4.3 Advanced QR Generator](#43-advanced-qr-generator)
    - [4.4 Linux Subsystem Integration](#44-linux-subsystem-integration)
    - [4.5 X++ Interpreter](#45-x-interpreter)
    - [4.6 Vim Editor Integration](#46-vim-editor-integration)
    - [4.7 Ruby Bridge](#47-ruby-bridge)
  - [5. Security Features](#5-security-features)
  - [6. Network and Web Functions](#6-network-and-web-functions)
  - [7. AI Integration (ClawBot - openclaw.ai)](#7-ai-integration-clawbot---openclawai)
  - [8. Professional Mode](#8-professional-mode)
- [Installation and Running](#installation-and-running)
- [Contact](#contact)

## Features

### 1. Core System Functions

OpenNCL provides basic command-line operations, enabling users to efficiently manage files, obtain system information, and execute common commands.

| Command        | Description                                     | Example Usage                               |
| :------------- | :---------------------------------------------- | :------------------------------------------ |
| `help`         | Displays help information for all available commands | `help`                                      |
| `exit`         | Exits the OpenNCL application                   | `exit`                                      |
| `dir`          | Lists files and folders in the current directory | `dir`                                       |
| `date`         | Displays the current date and time              | `date`                                      |
| `ip`           | Gets the public IP address of the current device | `ip`                                        |
| `color(fg,bg)` | Sets foreground and background colors for terminal text (partially supported) | `color(red,black)`                          |

### 2. System Shortcuts

OpenNCL integrates shortcuts for various Windows system tools, allowing users to launch them directly from the command line.

| Command        | Description                                     |
| :------------- | :---------------------------------------------- |
| `cmd`          | Launches Command Prompt                         |
| `powershell`   | Launches PowerShell                             |
| `explorer`     | Opens File Explorer                             |
| `notepad`      | Launches Notepad                                |
| `control`      | Opens Control Panel                             |
| `taskmgr`      | Launches Task Manager                           |
| `calc`         | Launches Calculator                             |
| `mspaint`      | Launches MS Paint                               |

### 3. Module Management

OpenNCL supports installing external modules from specified repositories to extend its functionality.

| Command                 | Description                                     | Example Usage                               |
| :---------------------- | :---------------------------------------------- | :------------------------------------------ |
| `install <module-name>` | Installs a module from the `TomLangModules` repository | `install my_utility`                        |

### 4. Advanced Tools

OpenNCL includes several practical tools, covering calculation, screenshot, QR code generation, Linux subsystem interaction, and programming interpreters.

#### 4.1 Enhanced Calculator

A feature-rich GUI scientific calculator supporting basic operations, scientific functions, and including a classic Windows Calculator easter egg.

#### 4.2 Screenshot Tool

A simple GUI screenshot tool supporting full-screen capture and area selection (area selection is simulated in the current sandbox environment).

#### 4.3 Advanced QR Generator

A powerful GUI QR code generation tool supporting custom content, error correction levels, versions, foreground/background colors, and embeddable logos, with batch generation capabilities.

#### 4.4 Linux Subsystem Integration

Through the `linux` command, users can interact with Windows Subsystem for Linux (WSL), execute Linux commands, or manage the WSL environment.

| Command                 | Description                                     | Example Usage                               |
| :---------------------- | :---------------------------------------------- | :------------------------------------------ |
| `linux`                 | Enters Linux subsystem interactive mode         | `linux`                                     |
| `shell`                 | Opens an interactive shell in WSL               | `linux` then type `shell`                   |
| `run <cmd...>`          | Runs a specified command in WSL                 | `linux` then type `run ls -l`               |
| `runelf <path> [args]`  | Runs an ELF executable in WSL                   | `linux` then type `runelf /usr/bin/htop`    |
| `install-wsl`           | Attempts to install WSL (requires admin privileges) | `linux` then type `install-wsl`             |

#### 4.5 X++ Interpreter

An embedded X++ programming language interpreter, allowing users to write and execute simple scripts in the command-line environment.

| Command                 | Description                                     | Example Usage                               |
| :---------------------- | :---------------------------------------------- | :------------------------------------------ |
| `X++` / `Xplusplus`     | Enters the X++ interactive programming environment | `X++`                                       |

#### 4.6 Vim Editor Integration

Through the `edit` command, users can directly invoke the integrated Vim editor within OpenNCL to edit files.

| Command                 | Description                                     | Example Usage                               |
| :---------------------- | :---------------------------------------------- | :------------------------------------------ |
| `edit [file]`           | Opens a file for editing, defaults to `untitled.xpp` | `edit my_script.py`                         |

#### 4.7 Ruby Bridge

Provides a Python-to-Ruby bridge service, allowing Python programs to communicate with Ruby scripts.

| Command                 | Description                                     | Example Usage                               |
| :---------------------- | :---------------------------------------------- | :------------------------------------------ |
| `bridge start`          | Starts the Ruby bridge server                   | `bridge start`                              |

### 5. Security Features

OpenNCL offers file encryption and decryption functionalities to protect user data.

| Command                 | Description                                     | Example Usage                               |
| :---------------------- | :---------------------------------------------- | :------------------------------------------ |
| `encrypt <path>`        | Encrypts a file or folder using the Fernet algorithm | `encrypt my_secret_file.txt`                |
| `decrypt <path>`        | Decrypts previously encrypted files or folders  | `decrypt my_secret_file.txt.enc`            |

### 6. Network and Web Functions

OpenNCL integrates various web search and URL opening functionalities.

| Command                       | Description                                     | Example Usage                               |
| :---------------------------- | :---------------------------------------------- | :------------------------------------------ |
| `translate <from> <to> <text>` | Translates text using Google Translate          | `translate en zh Hello World`               |
| `{search:Google}:<keyword>`   | Searches Google for the specified keyword       | `{search:Google}:OpenNCL features`          |
| `{search:Bing}:<keyword>`     | Searches Bing for the specified keyword         | `{search:Bing}:AI assistant`                |
| `{search:YouTube}:<keyword>`  | Searches YouTube for the specified keyword      | `{search:YouTube}:Python tutorial`          |
| `{open:<url>}`                | Opens the specified URL in the default browser  | `{open:https://www.openclaw.ai}`            |

### 7. AI Integration (ClawBot - openclaw.ai)

OpenNCL 3.1415926 introduces deep integration with **ClawBot (openclaw.ai)**, bringing powerful AI assistant capabilities to the command-line environment.

| Command                 | Description                                     | Example Usage                               |
| :---------------------- | :---------------------------------------------- | :------------------------------------------ |
| `claw ask <message>`    | Asks ClawBot AI a question to get an intelligent answer | `claw ask What is the capital of France?`   |
| `claw config <key> [url]` | Configures your ClawBot API Key and Gateway URL | `claw config YOUR_API_KEY`                  |
| `claw tools`            | Lists all available automation tools in your current OpenClaw instance | `claw tools`                                |
| `claw info`             | Displays the current ClawBot configuration status | `claw info`                                 |

### 8. Professional Mode

The `mode pro` command activates a "Professional Mode," where OpenNCL removes some system restrictions and provides simulated advanced functionalities. Use with caution.

| Command                 | Description                                     | Example Usage                               |
| :---------------------- | :---------------------------------------------- | :------------------------------------------ |
| `mode pro`              | Enters Professional Mode                        | `mode pro`                                  |
| `ping <host>`           | Simulates the ping command in Professional Mode | `ping google.com`                           |
| `open <path>`           | Opens a file or path in Professional Mode       | `open C:\Users\Document`                    |
| `encrypt <path>`        | Simulates file encryption in Professional Mode  | `encrypt my_data.txt`                       |
| `scan`                  | Simulates LAN device scanning in Professional Mode | `scan`                                      |

## Installation and Running

### Prerequisites

- Python 3.x
- Ruby (for Ruby Bridge functionality)
- Git (for module installation)


## Contact

- **Author**: ChenTom2016
- **Maintainer**: ChenTom2016
- **Last Updated**: March 14, 2026
