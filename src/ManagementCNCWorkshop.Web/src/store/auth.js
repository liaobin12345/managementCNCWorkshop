import { defineStore } from 'pinia'

export const useAuthStore = defineStore('auth', {
  state: () => ({
    token: localStorage.getItem('token') || '',
    employee: JSON.parse(localStorage.getItem('employee') || 'null')
  }),
  actions: {
    setLogin(token, employee) {
      this.token = token
      this.employee = employee
      localStorage.setItem('token', token)
      localStorage.setItem('employee', JSON.stringify(employee))
    },
    logout() {
      this.token = ''
      this.employee = null
      localStorage.removeItem('token')
      localStorage.removeItem('employee')
    }
  }
})
