## 说明
基于 .NET 6 的 Web API 服务，底层用 ONNX Runtime 推理引擎加载 PP-OCR 模型，实现图片文字识别和表格识别,提供可视化面板测试和API调用，开箱即用。

模型下载之后放到根目录新建文件夹Models下方，注意修改配置文件中的模型路径！

项目启动后自动进入可视化测试界面！



## 许可证

本项目基于 [PaddleOCR](https://github.com/PaddlePaddle/PaddleOCR) 构建，PaddleOCR 使用 Apache License 2.0 开源协议。

本项目的源代码遵循 **MIT License**（或你自己选的协议），但需保留对 PaddleOCR 的版权声明。

### 第三方依赖版权声明

本项目中使用的 PaddleOCR / PP-OCR 模型及其相关代码，版权归 [百度飞桨团队](https://github.com/PaddlePaddle) 所有，并遵循 Apache License 2.0 协议：
