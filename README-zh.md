# OpenNCL 简体中文版说明

( 简体中文 |[ English ]( README.md ) )

<b>多功能 Python 命令行工具</b><br>
<i>功能强大的多用途 Python 命令行应用</i>

<p align="center">
  <a href="https://www.python.org/"><img src="https://img.shields.io/badge/Python-3.8+-blue.svg"></a>
  <a href="LICENSE"><img src="https://img.shields.io/badge/License-MIT-green.svg"></a>
  <a href="https://github.com/chenTom2016/new-command/stargazers"><img src="https://img.shields.io/github/stars/chenTom2016/new-command.svg?style=social"></a>
  <a href="https://github.com/chenTom2016/new-command/issues"><img src="https://img.shields.io/github/issues/chenTom2016/new-command.svg"></a>
  <a href="https://github.com/chenTom2016/new-command/network/members"><img src="https://img.shields.io/github/forks/chenTom2016/new-command.svg"></a>
</p>

OpenNCL 是一个由 **Python** 和 **Ruby** 开发的多功能命令行应用程序。  
**OpenNCL** (Open New Command Line OS)

**作者：**ChenTom2016  
**运行环境：** Windows + Python + Ruby  
**最近更新：** 2026年2月5日

本项目采用 **LGPL** 与 **MIT** 许可证。

> [!TIP]
> 本应用支持 Linux 和 Windows，请根据需要选择适合的版本下载。

---

## 📑 目录导航

- [功能特性](#-功能特性)
- [快速开始](#-快速开始)
- [作者信息](#-作者信息)

---

## ✨ 功能特性

- 🖥 **命令行 Shell**
  - 内建命令：help、dir、date、ip、exit
  - 系统命令：python、node、cmd、powershell、notepad、explorer

- 📦 **模块安装器**
  - 类似于 pip，支持 `install <模块名>` 或通过 git URL 安装

- 🎨 **彩色输出**
  - 支持 `color(fg, bg)` 自定义显示颜色

- 🖼 **截图工具**
  - 支持全屏或区域截图，并有截图预览

- 📱 **进阶二维码工具**
  - 支持自定义颜色、嵌入LOGO、批量生成、历史记录

- 🧮 **增强计算器**
  - 支持科学计算、历史记录和存储操作
  - 包含 Windows 经典彩蛋 `2016 ÷ 13` 🎉

- 🔒 **文件加密与解密**
  - 基于 `cryptography.Fernet`，支持递归加密

- 🌐 **翻译工具**
  - 基于谷歌翻译，命令格式：`translate <源语言> <目标语言> <文本>`

- 🔍 **搜索与打开**
  - 示例：`{search:Google}: OpenAI`
  - 或 `{open:www.python.org}`

- ⚡ **专业模式**
  - 使用 `mode pro` 进入
  - 增加 `ping`、`open`、`encrypt`、`scan` 等命令支持

- 📝 **X++ 解释器**
  - 轻量级解释器，支持变量、表达式、条件判断及 REPL 交互

---

### 依赖库安装

```bash
pip install colorama pillow cryptography googletrans==3.1.0a0 requests qrcode
```

### 运行 OpenNCL

1. 下载并解压项目文件。
2. 进入项目根目录。
3. 运行主程序：

    ```bash
    python OpenNCL.py
    ```

---

## 🚀 快速开始

### 环境需求
- Python 3.8及以上

### 依赖库安装
```bash
pip install tkinter pillow qrcode cryptography googletrans==4.0.0-rc1 colorama requests
```

### 运行方法

**Windows：**
>
> ```bash
> python "OpenNCL.py"
> ```
> 或
> ```bash
> OpenNCL.exe
> ```
> 或
> ```bash
> OpenNCL.cmd
> ```

**Linux：**
>
> ```bash
> python "OpenNCL.py"
> ```

---

## 🙋‍♂️ 贡献指南

欢迎大家为 OpenNCL 提交功能建议、Bug 反馈或代码改进！  
可通过 Pull Request 或 Issue 参与贡献。

---

## 许可证

本项目基于 MIT 许可证开源，详见 `LICENSE.txt` 文件。

---

## 🛠 作者信息

- 作者：**Tom (chenTom2016)**
- GitHub: [chenTom2016](https://github.com/chenTom2016)
- 联系方式: **OpenNCL@outlook.com**

---

> 本网页最近更新于 **2026年2月18日**

---

此页面为AI翻译，可能翻译不正确。
