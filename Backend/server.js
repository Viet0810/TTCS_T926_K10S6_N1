const express = require('express');
const multer = require('multer');
const path = require('path');
const cors = require('cors');
const fs = require('fs');

const app = express();
const PORT = process.env.PORT || 5000;

// Cấu hình Middleware
app.use(cors());
app.use(express.json());

// Tự động tạo thư mục 'uploads' lưu trữ file nếu chưa tồn tại
const uploadDir = path.join(__dirname, 'uploads');
if (!fs.existsSync(uploadDir)) {
    fs.mkdirSync(uploadDir, { recursive: true });
}

// Cấu hình lưu trữ tệp tin với Multer
const storage = multer.diskStorage({
    destination: function (req, file, cb) {
        cb(null, uploadDir);
    },
    filename: function (req, file, cb) {
        const studentId = req.body.studentId || 'DTC245200226';
        const fileExt = path.extname(file.originalname);
        const timeStamp = Date.now();
        cb(null, `${file.fieldname}_${studentId}_${timeStamp}${fileExt}`);
    }
});

// Bộ lọc định dạng file (.pdf, .doc, .docx) và giới hạn 5MB
const fileFilter = (req, file, cb) => {
    const allowedExtensions = ['.pdf', '.doc', '.docx'];
    const ext = path.extname(file.originalname).toLowerCase();
    
    if (allowedExtensions.includes(ext)) {
        cb(null, true);
    } else {
        cb(new Error('Chỉ chấp nhận các file có định dạng .pdf, .doc, hoặc .docx!'), false);
    }
};

const upload = multer({
    storage: storage,
    limits: { fileSize: 5 * 1024 * 1024 }, // Tối đa 5MB
    fileFilter: fileFilter
});

// API Route: Tải lên CV và Đơn xin thực tập
app.post('/api/internship/upload', (req, res) => {
    const cpUpload = upload.fields([
        { name: 'cvFile', maxCount: 1 },
        { name: 'appFile', maxCount: 1 }
    ]);

    cpUpload(req, res, function (err) {
        if (err instanceof multer.MulterError) {
            if (err.code === 'LIMIT_FILE_SIZE') {
                return res.status(400).json({ 
                    success: false, 
                    message: 'Dung lượng file vượt quá giới hạn 5MB!' 
                });
            }
            return res.status(400).json({ 
                success: false, 
                message: `Lỗi upload: ${err.message}` 
            });
        } else if (err) {
            return res.status(400).json({ 
                success: false, 
                message: err.message 
            });
        }

        // Kiểm tra đủ cả 2 file
        if (!req.files || !req.files.cvFile || !req.files.appFile) {
            return res.status(400).json({ 
                success: false, 
                message: 'Vui lòng chọn đầy đủ cả file CV và Đơn xin thực tập!' 
            });
        }

        const cvFile = req.files.cvFile[0];
        const appFile = req.files.appFile[0];
        const studentId = req.body.studentId || 'DTC245200226';

        return res.status(200).json({
            success: true,
            message: 'Tải lên hồ sơ thực tập thành công!',
            data: {
                studentId: studentId,
                status: 'Đã hoàn thiện',
                files: {
                    cv: {
                        originalName: cvFile.originalname,
                        savedName: cvFile.filename,
                        size: `${(cvFile.size / (1024 * 1024)).toFixed(2)} MB`
                    },
                    application: {
                        originalName: appFile.originalname,
                        savedName: appFile.filename,
                        size: `${(appFile.size / (1024 * 1024)).toFixed(2)} MB`
                    }
                }
            }
        });
    });
});
// Route kiểm tra trạng thái Server
app.get('/', (req, res) => {
    res.send('🚀 Backend API đang chạy bình thường!');
});
// Khởi chạy Server
app.listen(PORT, () => {
    console.log(`🚀 Server Backend đang chạy tại http://localhost:${PORT}`);
});