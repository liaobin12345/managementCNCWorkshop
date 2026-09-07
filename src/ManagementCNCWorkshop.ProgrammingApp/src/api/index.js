import { get, post, put, del, upload } from './request'

// ── 认证（复用车间后端账号体系） ──
export const apiLogin = (data) => post('/auth/login', data)
export const apiWechatLogin = (code) => post('/auth/wechat-login', { code })
export const apiWechatBind = (data) => post('/auth/wechat-bind', data)
export const apiMe = () => get('/worker/me')

// ── 机床 ──
export const apiMachines = (active) => get('/programming/machines', active ? { active } : {})
export const apiCreateMachine = (data) => post('/programming/machines', data)
export const apiUpdateMachine = (id, data) => put(`/programming/machines/${id}`, data)
export const apiDeleteMachine = (id) => del(`/programming/machines/${id}`)

// ── 材料库（含推荐切削参数） ──
export const apiMaterials = () => get('/programming/materials')

// ── 程序 ──
export const apiPrograms = (mine, status) =>
  get('/programming/programs', {
    ...(mine ? { mine: 'true' } : {}),
    ...(status ? { status } : {}),
  })
export const apiProgramDetail = (id) => get(`/programming/programs/${id}`)
export const apiCreateProgram = (data) => post('/programming/programs', data)
export const apiUpdateProgram = (id, data) => put(`/programming/programs/${id}`, data)
export const apiSavePoints = (id, points) => put(`/programming/programs/${id}/points`, points)
export const apiConfirmProgram = (id) => post(`/programming/programs/${id}/confirm`)
export const apiProcessPlan = (id) => get(`/programming/programs/${id}/process-plan`)
export const apiGenerateProgram = (id) => post(`/programming/programs/${id}/generate`)
export const apiDeleteProgram = (id) => del(`/programming/programs/${id}`)
export const apiParseDxf = (filePath) => upload('/programming/dxf-parse', filePath, 'file')
